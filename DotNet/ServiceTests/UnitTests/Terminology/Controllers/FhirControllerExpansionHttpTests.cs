using System.Net;
using System.Text.Json;
using LantanaGroup.Link.Terminology.Application.Formatters;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Controllers;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Terminology;

/// <summary>
/// Exercises the expansion paging parameters over real HTTP, through the same model binding and MVC
/// options <c>Program.cs</c> configures (LEGLINK-968).
/// </summary>
/// <remarks>
/// Calling the action directly cannot cover this. The point of these tests is that <c>count</c> and
/// <c>offset</c> bind from the query string under those exact names -- FHIR spells the expand paging
/// parameters without a leading underscore, and a rename to <c>_count</c> would leave every request
/// silently on the default page while a direct call kept passing. The 400 for a non-numeric value is
/// produced by the ApiController model-state filter before the action runs, which likewise only exists
/// over HTTP.
///
/// As in <see cref="FhirControllerHttpTests"/>, the MVC options below mirror <c>Program.cs</c> by hand
/// rather than booting the real host, whose startup needs Kafka, the cache and App Configuration. The
/// two must be kept in step.
/// </remarks>
public class FhirControllerExpansionHttpTests
{
    private const string ValueSetUrl = "http://hl7.org/fhir/ValueSet/paged";
    private const string CodeSystemUrl = "http://hl7.org/fhir/paged";
    private const string ValueSetId = "paged";

    /// <summary>
    /// Bounds small enough that four codes are enough to reach a page boundary and the maximum.
    /// </summary>
    private const int DefaultPageSize = 2;
    private const int MaxPageSize = 3;

    private static TestServer BuildServer()
    {
        var codes = new List<Code>
        {
            new() { Value = "one", Display = "One" },
            new() { Value = "two", Display = "Two" },
            new() { Value = "three", Display = "Three" },
            new() { Value = "four", Display = "Four" }
        };

        var valueSet = new CodeGroup
        {
            Id = ValueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Url = ValueSetUrl,
            Resource = new ValueSet { Id = ValueSetId, Url = ValueSetUrl },
            Codes = new Dictionary<string, List<Code>> { { CodeSystemUrl, codes } }
        };

        var cache = new Mock<ICodeGroupCacheService>();
        cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(valueSet);
        cache.Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, It.IsAny<string>()))
            .Returns(valueSet);

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(cache.Object);
                services.AddSingleton(Mock.Of<ITerminologyServiceMetrics>());
                services.AddSingleton(TerminologyTestConfig.Options(DefaultPageSize, MaxPageSize));
                services.AddSingleton<FhirService>();
                services.AddControllers(options =>
                    {
                        options.ModelBinderProviders.Insert(0, new FhirModelBinderProvider());
                        options.OutputFormatters.Insert(0, new FhirOutputFormatter());
                        options.ModelMetadataDetailsProviders.Add(new PreserveEmptyStringMetadataProvider());
                    })
                    .AddApplicationPart(typeof(FhirController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        return new TestServer(builder);
    }

    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(string path)
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync(path);

        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private const string ExpandPath = "/api/terminology/fhir/ValueSet/$expand";
    private const string SearchPath = "/api/terminology/fhir/ValueSet";

    private static List<string> ExpansionCodes(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement
            .GetProperty("expansion")
            .GetProperty("contains")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("code").GetString()!)
            .ToList();
    }

    private static int ExpansionTotal(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("expansion").GetProperty("total").GetInt32();
    }

    [Fact]
    public async Task Expand_WithCountAndOffset_BindsBothAndPages()
    {
        var (status, body) = await GetAsync(ExpandPath + "?url=" + ValueSetUrl + "&count=2&offset=1");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new[] { "two", "three" }, ExpansionCodes(body).ToArray());
        Assert.Equal(4, ExpansionTotal(body));
    }

    [Fact]
    public async Task Expand_WithNoPagingParameters_AppliesTheDefaultPage()
    {
        var (status, body) = await GetAsync(ExpandPath + "?url=" + ValueSetUrl);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(DefaultPageSize, ExpansionCodes(body).Count);
        Assert.Equal(4, ExpansionTotal(body));
    }

    [Fact]
    public async Task Expand_WithCountAboveTheMaximum_ClampsRatherThanFailing()
    {
        var (status, body) = await GetAsync(ExpandPath + "?url=" + ValueSetUrl + "&count=999999");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(MaxPageSize, ExpansionCodes(body).Count);
    }

    /// <summary>
    /// The parameter is count, not _count. An underscored value is an unrecognised query parameter and
    /// must not page, or a client using the search spelling would silently get a different page size
    /// from the one it asked for.
    /// </summary>
    [Fact]
    public async Task Expand_WithUnderscoreCount_IsIgnored()
    {
        var (status, body) = await GetAsync(ExpandPath + "?url=" + ValueSetUrl + "&_count=1");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(DefaultPageSize, ExpansionCodes(body).Count);
    }

    [Fact]
    public async Task Expand_WithNegativeCount_Returns400()
    {
        var (status, body) = await GetAsync(ExpandPath + "?url=" + ValueSetUrl + "&count=-1");

        Assert.Equal(HttpStatusCode.BadRequest, status);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            "The 'count' parameter cannot be negative.",
            document.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>
    /// A non-numeric count never reaches the action: the ApiController model-state filter rejects it
    /// first. The body is a ValidationProblemDetails rather than the controller's own Problem Details,
    /// but both are RFC 9457 and this is already how a malformed _summary behaves.
    /// </summary>
    [Fact]
    public async Task Expand_WithNonNumericCount_Returns400FromModelBinding()
    {
        var (status, body) = await GetAsync(ExpandPath + "?url=" + ValueSetUrl + "&count=abc");

        Assert.Equal(HttpStatusCode.BadRequest, status);

        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("count", out _));
    }

    [Fact]
    public async Task ValueSetSearch_WithSummaryTrueAndCount_ReturnsNoExpansion()
    {
        var (status, body) = await GetAsync(SearchPath + "?url=" + ValueSetUrl + "&_summary=true&count=1");

        Assert.Equal(HttpStatusCode.OK, status);

        using var document = JsonDocument.Parse(body);
        var resource = document.RootElement.GetProperty("entry")[0].GetProperty("resource");

        Assert.False(resource.TryGetProperty("expansion", out _));
    }

    [Fact]
    public async Task ValueSetSearch_WithoutSummary_BindsCountAndOffset()
    {
        var (status, body) = await GetAsync(SearchPath + "?url=" + ValueSetUrl + "&count=1&offset=3");

        Assert.Equal(HttpStatusCode.OK, status);

        using var document = JsonDocument.Parse(body);
        var expansion = document.RootElement.GetProperty("entry")[0].GetProperty("resource").GetProperty("expansion");

        Assert.Equal(4, expansion.GetProperty("total").GetInt32());
        Assert.Equal(3, expansion.GetProperty("offset").GetInt32());

        // The nested grouper shape: one entry per code system, carrying the page's codes as children.
        var grouper = expansion.GetProperty("contains")[0];
        Assert.Equal(CodeSystemUrl, grouper.GetProperty("system").GetString());
        Assert.Equal("four", grouper.GetProperty("contains")[0].GetProperty("code").GetString());
    }
}
