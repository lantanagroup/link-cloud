using System.Net;
using System.Text.Json;
using Hl7.Fhir.Model;
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
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Terminology;

/// <summary>
/// A FHIR client escapes <c>|</c>, <c>,</c>, <c>$</c> and <c>\</c> in search values with a backslash, so HAPI
/// searches for a version-pinned canonical as <c>url=...us-core-vital-signs\|6.1.0</c>. The search endpoints
/// must undo that, or Validation treats every such value set as unknown and fails every code in it.
/// </summary>
public class FhirControllerSearchEscapingHttpTests
{
    private const string ValueSetUrl = "http://hl7.org/fhir/us/core/ValueSet/us-core-vital-signs";
    private const string CodeSystemUrl = "http://example.org/CodeSystem/with,comma";

    /// <summary>
    /// The cache answers only for the exact, unescaped identifiers, as the real one does: an escaped value never
    /// matches a cached URL.
    /// </summary>
    private static TestServer BuildServer()
    {
        var cache = new Mock<ICodeGroupCacheService>();
        cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl + "|6.1.0", It.IsAny<string>()))
            .Returns(new CodeGroup
            {
                Id = "us-core-vital-signs",
                Type = CodeGroup.CodeGroupTypes.ValueSet,
                Url = ValueSetUrl,
                Version = "6.1.0",
                Resource = new ValueSet { Url = ValueSetUrl, Version = "6.1.0" }
            });
        cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemUrl, It.IsAny<string>()))
            .Returns(new CodeGroup
            {
                Id = "with-comma",
                Type = CodeGroup.CodeGroupTypes.CodeSystem,
                Url = CodeSystemUrl,
                Resource = new CodeSystem { Url = CodeSystemUrl }
            });

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddAntiforgery();
                services.AddSingleton(cache.Object);
                services.AddSingleton(Mock.Of<ITerminologyServiceMetrics>());
                services.AddSingleton(TerminologyTestConfig.Options());
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

    private static async Task<int> SearchTotalAsync(string resourceType, string encodedUrl)
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync($"/api/terminology/fhir/{resourceType}?url={encodedUrl}&_summary=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("entry", out var entries) ? entries.GetArrayLength() : 0;
    }

    [Fact]
    public async Task ValueSet_search_with_an_escaped_version_pipe_finds_the_value_set()
    {
        // What HAPI's client sends: the pipe escaped with a backslash, then percent-encoded.
        var total = await SearchTotalAsync("ValueSet", Uri.EscapeDataString(ValueSetUrl) + "%5C%7C6.1.0");

        Assert.Equal(1, total);
    }

    [Fact]
    public async Task ValueSet_search_with_an_unescaped_version_pipe_still_finds_the_value_set()
    {
        var total = await SearchTotalAsync("ValueSet", Uri.EscapeDataString(ValueSetUrl + "|6.1.0"));

        Assert.Equal(1, total);
    }

    [Fact]
    public async Task CodeSystem_search_unescapes_its_url_too()
    {
        var total = await SearchTotalAsync("CodeSystem", Uri.EscapeDataString(CodeSystemUrl.Replace(",", "\\,")));

        Assert.Equal(1, total);
    }

    [Theory]
    [InlineData(@"a\|b", "a|b")]
    [InlineData(@"a\,b", "a,b")]
    [InlineData(@"a\$b", "a$b")]
    [InlineData(@"a\\b", @"a\b")]
    [InlineData(@"a\\\|b", @"a\|b")]
    [InlineData(@"a\xb", @"a\xb")]
    [InlineData(@"trailing\", @"trailing\")]
    [InlineData("plain", "plain")]
    public void Search_values_are_unescaped_as_the_FHIR_spec_defines(string escaped, string expected)
    {
        Assert.Equal(expected, FhirController.UnescapeSearchValue(escaped));
    }
}
