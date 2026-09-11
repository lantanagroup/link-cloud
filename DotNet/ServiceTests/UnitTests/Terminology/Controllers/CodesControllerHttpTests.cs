using System.Net;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
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
/// Exercises <c>GET /api/terminology/codes</c> over real HTTP, through the same model binding and MVC
/// options <c>Program.cs</c> configures.
/// </summary>
/// <remarks>
/// Calling the action directly cannot cover the cases that matter most here. The sanitizing happens in the
/// bound model's setters, the response shape depends on which output formatter claims the result, and the
/// validation errors are only assembled once <c>ProblemDetailsFactory</c> has run — all of which a direct
/// call skips.
///
/// The MVC options below mirror <c>Program.cs</c> by hand rather than booting the real host, whose startup
/// needs the cache, telemetry and App Configuration. The two must be kept in step.
/// </remarks>
public class CodesControllerHttpTests
{
    private const string Endpoint = "/api/terminology/codes";
    private const string Hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";

    /// <summary>
    /// Stands the controller up behind a real pipeline over a cache holding one code system. The display
    /// carries an ampersand deliberately: it is the character that distinguishes a correct sanitizer from
    /// one that quietly destroys the value it is cleaning.
    /// </summary>
    private static TestServer BuildServer()
    {
        var cache = new Mock<ICodeGroupCacheService>();

        // The unscoped search asks for every loaded group; a loose mock would hand back null and fault.
        cache.Setup(x => x.GetAllCodeGroups(It.IsAny<CodeGroup.CodeGroupTypes>()))
            .Returns(() => []);

        cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, Hsloc, It.IsAny<string>()))
            .Returns(new CodeGroup
            {
                Id = "hsloc",
                Url = Hsloc,
                Version = "1.0.0",
                Type = CodeGroup.CodeGroupTypes.CodeSystem,
                Codes = new Dictionary<string, List<Code>>
                {
                    {
                        Hsloc,
                        [
                            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" },
                            new CodeSystemCode { Value = "1096-7", Display = "Labor & Delivery Ward" },
                            new CodeSystemCode
                            {
                                Value = "1097-5", Display = "Retired Ward", Status = CodeStatus.Inactive
                            }
                        ]
                    }
                }
            });

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(cache.Object);
                services.AddSingleton<FhirService>();
                services.AddSingleton<ICodeSearchService, CodeSearchService>();
                services.AddControllers(options =>
                    {
                        options.ModelBinderProviders.Insert(0, new FhirModelBinderProvider());
                        options.OutputFormatters.Insert(0, new FhirOutputFormatter());
                        options.ModelMetadataDetailsProviders.Add(new PreserveEmptyStringMetadataProvider());
                    })
                    .AddApplicationPart(typeof(CodesController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        return new TestServer(builder);
    }

    private static async Task<(HttpStatusCode Status, JsonDocument Body)> GetAsync(string query)
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync($"{Endpoint}{query}");
        var body = await response.Content.ReadAsStringAsync();

        return (response.StatusCode, JsonDocument.Parse(body));
    }

    private static string Encoded(string value) => Uri.EscapeDataString(value);

    /// <summary>
    /// Asserts the Problem Details carries an error against the named query parameter, reporting the whole
    /// payload when it does not - a missing key is otherwise indistinguishable from a misspelled one.
    /// </summary>
    private static void AssertErrorFor(string parameter, JsonDocument body)
    {
        Assert.True(
            body.RootElement.TryGetProperty("errors", out var errors) &&
            errors.TryGetProperty(parameter, out _),
            $"Expected an error for '{parameter}'. Body was: {body.RootElement.GetRawText()}");
    }

    // ------------------------------------------------------------- success

    /// <summary>
    /// The response is the repo's standard search shape, and <c>status</c> is a string rather than the
    /// enum's integer — the mapping UI reads it directly.
    /// </summary>
    [Fact]
    public async Task Search_ReturnsThePagedShapeWithStatusAsAString()
    {
        var (status, body) = await GetAsync($"?search=burn&codeSystem={Encoded(Hsloc)}");

        Assert.Equal(HttpStatusCode.OK, status);

        var record = Assert.Single(body.RootElement.GetProperty("records").EnumerateArray().ToList());
        Assert.Equal(Hsloc, record.GetProperty("system").GetString());
        Assert.Equal("1026-4", record.GetProperty("code").GetString());
        Assert.Equal("Burn Critical Care", record.GetProperty("display").GetString());
        Assert.Equal(JsonValueKind.String, record.GetProperty("status").ValueKind);
        Assert.Equal("Active", record.GetProperty("status").GetString());

        var metadata = body.RootElement.GetProperty("metadata");
        Assert.Equal(1, metadata.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, metadata.GetProperty("pageNumber").GetInt32());
        Assert.Equal(CodeSearchDefaults.DefaultPageSize, metadata.GetProperty("pageSize").GetInt32());
    }

    /// <summary>
    /// A canonical URI survives sanitizing. <c>SanitizeAndRemove</c> would strip its ":" and "/", leaving a
    /// URI that matches nothing, and every scoped search would answer 400.
    /// </summary>
    [Fact]
    public async Task Search_CodeSystemUri_SurvivesSanitizing()
    {
        var (status, body) = await GetAsync($"?codeSystem={Encoded(Hsloc)}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(3, body.RootElement.GetProperty("metadata").GetProperty("totalCount").GetInt32());
    }

    /// <summary>
    /// An ampersand in the search text has to reach the matcher as "&amp;". Sanitizing HTML-encodes it and
    /// the decode restores it; without the decode this search matches nothing, which is the failure
    /// FhirController records for over 2,000 LOINC displays.
    /// </summary>
    [Fact]
    public async Task Search_AmpersandInSearchText_MatchesADisplayContainingOne()
    {
        var (status, body) = await GetAsync(
            $"?search={Encoded("Labor & Delivery")}&codeSystem={Encoded(Hsloc)}");

        Assert.Equal(HttpStatusCode.OK, status);

        var record = Assert.Single(body.RootElement.GetProperty("records").EnumerateArray().ToList());
        Assert.Equal("1096-7", record.GetProperty("code").GetString());
    }

    /// <summary>
    /// A search that matches nothing is still a successful search: it returns the metadata clients need to
    /// render paging rather than a 204 with no body.
    /// </summary>
    [Fact]
    public async Task Search_NoMatches_Returns200WithAnEmptyRecordsArray()
    {
        var (status, body) = await GetAsync($"?search=nosuchthing&codeSystem={Encoded(Hsloc)}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(body.RootElement.GetProperty("records").EnumerateArray().ToList());
        Assert.Equal(0, body.RootElement.GetProperty("metadata").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Search_ExcludeInactive_OmitsInactiveCodes()
    {
        var (_, included) = await GetAsync($"?codeSystem={Encoded(Hsloc)}");
        var (status, excluded) = await GetAsync($"?codeSystem={Encoded(Hsloc)}&excludeInactive=true");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(3, included.RootElement.GetProperty("metadata").GetProperty("totalCount").GetInt32());
        Assert.Equal(2, excluded.RootElement.GetProperty("metadata").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Search_PageSizeAboveTheMaximum_IsClampedRatherThanRefused()
    {
        var (status, body) = await GetAsync($"?codeSystem={Encoded(Hsloc)}&pageSize=5000");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(
            CodeSearchDefaults.MaxPageSize,
            body.RootElement.GetProperty("metadata").GetProperty("pageSize").GetInt32());
    }

    // ------------------------------------------------------------- rejection

    /// <summary>
    /// A request naming none of the three would page the entire loaded terminology set.
    /// </summary>
    [Fact]
    public async Task Search_WithNoConstraint_IsRefused()
    {
        var (status, body) = await GetAsync(string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertErrorFor(CodeSearchParameters.Search, body);
    }

    [Fact]
    public async Task Search_ShorterThanThreeCharacters_IsRefused()
    {
        var (status, body) = await GetAsync("?search=bu");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertErrorFor(CodeSearchParameters.Search, body);
    }

    /// <summary>
    /// The parameter is named the way the caller spelled it in the query string, not the way the property
    /// is spelled in C#.
    /// </summary>
    [Fact]
    public async Task Search_CodeSystemAndValueSetTogether_IsRefusedNamingBothParameters()
    {
        var (status, body) = await GetAsync(
            $"?codeSystem={Encoded(Hsloc)}&valueSet={Encoded("http://example.org/ValueSet/x")}");

        Assert.Equal(HttpStatusCode.BadRequest, status);

        AssertErrorFor(CodeSearchParameters.CodeSystem, body);
        AssertErrorFor(CodeSearchParameters.ValueSet, body);
    }

    [Fact]
    public async Task Search_VersionWithoutACodeSystemOrValueSet_IsRefused()
    {
        var (status, body) = await GetAsync("?search=burn&version=1.0.0");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertErrorFor(CodeSearchParameters.Version, body);
    }

    /// <summary>
    /// A value that survives binding but sanitizes away to nothing is rejected rather than treated as
    /// omitted: dropping it would widen the search to every loaded code group, answering a question the
    /// caller did not ask. Markup-only input is not whitespace, so it reaches the action intact.
    /// </summary>
    [Fact]
    public async Task Search_CodeSystemThatSanitizesAwayToNothing_IsRefused()
    {
        // A script tag, not a <b>: the sanitizer allows benign formatting tags, so those survive and the
        // request would then be refused for naming an unloaded URI - passing, but for the wrong reason.
        var (status, body) = await GetAsync($"?search=burn&codeSystem={Encoded("<script>alert(1)</script>")}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertErrorFor(CodeSearchParameters.CodeSystem, body);
    }

    /// <summary>
    /// Pins the limit of that guard so it is a recorded decision rather than a surprise: MVC converts an
    /// empty or whitespace query value to null before the action runs, so those are indistinguishable
    /// from an omitted parameter and the search widens to all loaded content instead of failing.
    /// Closing the gap needs <c>PreserveEmptyStringAttribute</c> to be usable on a property, which it is
    /// not today - it targets parameters only.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("%20")]
    public async Task Search_EmptyOrWhitespaceCodeSystem_IsTreatedAsOmitted(string value)
    {
        var (status, body) = await GetAsync($"?search=burn&codeSystem={value}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(body.RootElement.GetProperty("records").EnumerateArray().ToList());
    }

    [Fact]
    public async Task Search_UnloadedCodeSystem_IsRefusedNamingTheParameter()
    {
        var (status, body) = await GetAsync($"?codeSystem={Encoded("http://example.org/CodeSystem/nope")}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertErrorFor(CodeSearchParameters.CodeSystem, body);
    }

    /// <summary>
    /// ArgumentException appends " (Parameter 'x')" to its message. That is framework detail, and the
    /// parameter is already the error's key, so it must not reach the caller.
    /// </summary>
    [Fact]
    public async Task Search_UnloadedCodeSystem_DoesNotLeakTheArgumentExceptionSuffix()
    {
        var (status, body) = await GetAsync($"?codeSystem={Encoded("http://example.org/CodeSystem/nope")}");

        Assert.Equal(HttpStatusCode.BadRequest, status);

        var message = body.RootElement
            .GetProperty("errors")
            .GetProperty(CodeSearchParameters.CodeSystem)[0]
            .GetString();

        Assert.DoesNotContain("(Parameter", message);
        Assert.Contains("http://example.org/CodeSystem/nope", message);
    }

    /// <summary>
    /// The cache answers an unloaded version with the latest one, so this asserts a refusal rather than a
    /// page of the version the caller did not ask for.
    /// </summary>
    [Fact]
    public async Task Search_UnloadedVersion_IsRefusedRatherThanServedFromTheLatest()
    {
        var (status, body) = await GetAsync($"?codeSystem={Encoded(Hsloc)}&version=9.9.9");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertErrorFor(CodeSearchParameters.Version, body);
    }
}
