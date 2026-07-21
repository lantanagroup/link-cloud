using LantanaGroup.Link.Terminology.Application.Extensions;
using LantanaGroup.Link.Terminology.Application.Formatters;
using LantanaGroup.Link.Terminology.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Terminology;

[Trait("Category", "IntegrationTests")]
public class TerminologyStatusCodePagesTests : IClassFixture<TerminologyStatusCodePagesFactory>
{
    private readonly TerminologyStatusCodePagesFactory _factory;

    public TerminologyStatusCodePagesTests(TerminologyStatusCodePagesFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnknownTerminologyRoute_ReturnsNotFoundProblemDetailsWithTraceId()
    {
        var client = _factory.CreateClient();
        using var content = new StringContent(
            """
            {
              "resourceType": "Parameters",
              "parameter": [{
                "name": "url",
                "valueUri": "http://terminology.hl7.org/CodeSystem/v3-ActCode"
              }, {
                "name": "code",
                "valueCode": "WRKCOMP"
              }]
            }
            """,
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/terminology/blah/CodeSystem/$validate-code", content);
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonNode.Parse(body)!.AsObject();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", problem["type"]!.GetValue<string>());
        Assert.Equal("Not Found", problem["title"]!.GetValue<string>());
        Assert.Equal(StatusCodes.Status404NotFound, problem["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(problem["detail"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]?.GetValue<string>()));
    }
}

public sealed class TerminologyStatusCodePagesFactory : WebApplicationFactory<TerminologyStatusCodePagesFactory.ApiTestMarker>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddTerminologyProblemDetails(context.HostingEnvironment);

                        services
                            .AddControllers(options =>
                            {
                                options.ModelBinderProviders.Insert(0, new FhirModelBinderProvider());
                                options.OutputFormatters.Insert(0, new FhirOutputFormatter());
                            })
                            .AddApplicationPart(typeof(FhirController).Assembly);
                    })
                    .Configure(app =>
                    {
                        app.UseStatusCodePages();
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            });
    }

    public sealed class ApiTestMarker { }
}
