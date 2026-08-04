using System.Net;
using System.Text;
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
/// Exercises ValueSet $validate-code over real HTTP, through the same model binding and MVC options
/// <c>Program.cs</c> configures, for the four blank-system requests QA covers in TestRail 10992, 11015,
/// 11342 and 11343 (LEGLINK-888).
/// </summary>
/// <remarks>
/// Calling the action method directly cannot cover these. MVC converts an empty query value to null before
/// an action runs unless <see cref="PreserveEmptyStringMetadataProvider"/> is registered, so a direct call
/// passing <c>string.Empty</c> exercises a state real traffic could not produce and would report a blank
/// <c>?system=</c> as rejected while the deployed service answered 200.
///
/// The MVC options below mirror <c>Program.cs</c> by hand rather than booting the real host, whose startup
/// needs Kafka, the cache and App Configuration. The two must be kept in step: dropping the metadata
/// provider from <c>Program.cs</c> alone would break the deployed service without failing these tests.
/// </remarks>
public class FhirControllerHttpTests
{
    private const string ValueSetUrl = "http://hl7.org/fhir/ValueSet/address-type";
    private const string CodeSystemUrl = "http://hl7.org/fhir/address-type";
    private const string Endpoint = "/api/terminology/fhir/ValueSet/$validate-code";

    /// <summary>
    /// Stands up the controller behind a real request pipeline, mirroring the MVC configuration in
    /// <c>Program.cs</c>. The value set is populated so a request that still fails did so on the system
    /// rather than on a failed lookup.
    /// </summary>
    private static TestServer BuildServer()
    {
        var cache = new Mock<ICodeGroupCacheService>();
        cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(new CodeGroup
            {
                Id = "address-type",
                Type = CodeGroup.CodeGroupTypes.ValueSet,
                Url = ValueSetUrl,
                Codes = new Dictionary<string, List<Code>>
                {
                    { CodeSystemUrl, new List<Code> { new() { Value = "postal", Display = "Postal" } } }
                }
            });

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(cache.Object);
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

    private static async Task<(HttpStatusCode Status, string Body)> PostAsync(string query, string body)
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{Endpoint}{query}", content);

        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static void AssertBadRequestDetail(HttpStatusCode status, string body, string expectedDetail)
    {
        Assert.Equal(HttpStatusCode.BadRequest, status);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", root.GetProperty("type").GetString());
        Assert.Equal("Bad Request", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedDetail, root.GetProperty("detail").GetString());
    }

    /// <summary>TestRail 10992 — blank system inside the body's coding.</summary>
    [Fact]
    public async Task ValidateCodeInValueSet_BlankSystemInCodingBody_Returns400()
    {
        const string body = """
        {
          "resourceType" : "Parameters",
          "parameter" : [{
            "name": "url",
            "valueUri": "http://hl7.org/fhir/ValueSet/address-type"
          }, {
            "name" : "coding",
            "valueCoding": { "code": "postal", "system": "" }
          }]
        }
        """;

        var (status, payload) = await PostAsync(string.Empty, body);

        AssertBadRequestDetail(status, payload, "The 'coding.system' parameter cannot be blank.");
    }

    /// <summary>TestRail 11015 — blank system on the query string, valid system in the body.</summary>
    [Fact]
    public async Task ValidateCodeInValueSet_BlankSystemQueryParameter_Returns400()
    {
        const string body = """
        {
          "resourceType" : "Parameters",
          "parameter" : [{
            "name": "url",
            "valueUri": "http://hl7.org/fhir/ValueSet/address-type"
          }, {
            "name" : "coding",
            "valueCoding": { "code": "postal", "system": "http://hl7.org/fhir/address-type" }
          }]
        }
        """;

        var (status, payload) = await PostAsync("?system=", body);

        AssertBadRequestDetail(status, payload, "The 'system' parameter cannot be blank.");
    }

    /// <summary>TestRail 11342 — blank system as a top-level body parameter.</summary>
    [Fact]
    public async Task ValidateCodeInValueSet_BlankSystemParameterInBody_Returns400()
    {
        const string body = """
        {
          "resourceType" : "Parameters",
          "parameter" : [{
            "name": "url",
            "valueUri": "http://hl7.org/fhir/ValueSet/address-type"
          }, {
            "name" : "code",
            "valueCode": "postal"
          }, {
            "name": "system",
            "valueUri": ""
          }]
        }
        """;

        var (status, payload) = await PostAsync(string.Empty, body);

        AssertBadRequestDetail(status, payload, "The 'system' parameter cannot be blank.");
    }

    /// <summary>TestRail 11343 — blank system inside the body's codeableConcept.</summary>
    [Fact]
    public async Task ValidateCodeInValueSet_BlankSystemInCodeableConcept_Returns400()
    {
        const string body = """
        {
          "resourceType" : "Parameters",
          "parameter" : [{
            "name": "url",
            "valueUri": "http://hl7.org/fhir/ValueSet/address-type"
          }, {
            "name" : "codeableConcept",
            "valueCodeableConcept": {
              "coding": [{ "code": "postal", "system": "" }]
            }
          }]
        }
        """;

        var (status, payload) = await PostAsync(string.Empty, body);

        AssertBadRequestDetail(status, payload, "The 'codeableConcept.coding.system' parameter cannot be blank.");
    }

    /// <summary>
    /// An omitted system keeps its FHIR meaning of "search every code system in the value set", so the
    /// rejection above must not be reached by simply leaving the parameter out.
    /// </summary>
    [Fact]
    public async Task ValidateCodeInValueSet_AbsentSystem_SearchesAllSystemsAndSucceeds()
    {
        const string body = """
        {
          "resourceType" : "Parameters",
          "parameter" : [{
            "name": "url",
            "valueUri": "http://hl7.org/fhir/ValueSet/address-type"
          }, {
            "name" : "coding",
            "valueCoding": { "code": "postal" }
          }]
        }
        """;

        var (status, payload) = await PostAsync(string.Empty, body);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("\"result\"", payload);
        Assert.Contains("true", payload);
    }

    /// <summary>
    /// Per LEGLINK-888, a client that interpolated an unset variable into the query string is treated as
    /// having omitted the parameter. This is deliberate non-FHIR leniency, so it is pinned by a test.
    /// </summary>
    [Theory]
    [InlineData("?system=null")]
    [InlineData("?system=undefined")]
    public async Task ValidateCodeInValueSet_PlaceholderSystem_TreatedAsAbsent(string query)
    {
        const string body = """
        {
          "resourceType" : "Parameters",
          "parameter" : [{
            "name": "url",
            "valueUri": "http://hl7.org/fhir/ValueSet/address-type"
          }, {
            "name" : "code",
            "valueCode": "postal"
          }]
        }
        """;

        var (status, payload) = await PostAsync(query, body);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("\"result\"", payload);
    }
}
