using System.Net;
using System.Text.Json.Nodes;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;
using LantanaGroup.Link.Shared.Application.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Nhsn
{
    /// <summary>
    /// The NHSN App BFF answers errors with RFC 7807 ProblemDetails carrying a traceId, as the API
    /// guidance requires, rather than an ad-hoc { "message": ... } body.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class NhsnErrorResponseShapeTests
    {
        private static async Task<WebApplication> AHost(LocalizationResourceResult localizationResult)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
            builder.WebHost.UseTestServer();

            builder.Services.AddProblemDetailsService(options =>
            {
                options.Environment = builder.Environment;
                options.ServiceName = "NHSN-App-BFF";
            });

            var localization = new Mock<ILocalizationResourceService>();
            localization
                .Setup(s => s.GetNamespaceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(localizationResult);
            builder.Services.AddSingleton(localization.Object);

            var app = builder.Build();
            // Same order as the BFF's Program.SetupMiddleware.
            app.UseExceptionHandler();
            app.UseStatusCodePages();
            app.UseRouting();
            new LocalizationEndpoints().RegisterEndpoints(app);

            await app.StartAsync();
            return app;
        }

        private static async Task<(HttpResponseMessage Response, JsonObject Problem)> Get(WebApplication app, string path)
        {
            var response = await app.GetTestClient().GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            return (response, JsonNode.Parse(body)!.AsObject());
        }

        private static void AssertProblemDetails(HttpResponseMessage response, JsonObject problem, HttpStatusCode expectedStatus)
        {
            Assert.Equal(expectedStatus, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("https://tools.ietf.org/html/rfc", problem["type"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(problem["title"]?.GetValue<string>()));
            Assert.Equal((int)expectedStatus, problem["status"]!.GetValue<int>());
            Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]?.GetValue<string>()));
            Assert.False(problem.ContainsKey("message"));
        }

        [Fact]
        public async Task InvalidLocale_ReturnsBadRequestProblemDetails()
        {
            await using var app = await AHost(new LocalizationResourceResult(
                LocalizationResourceStatus.InvalidLocale, Message: "The requested locale is not valid."));

            var (response, problem) = await Get(app, "/api/localization/xx-INVALID/common");

            AssertProblemDetails(response, problem, HttpStatusCode.BadRequest);
            Assert.Equal("The requested locale is not valid.", problem["detail"]!.GetValue<string>());
        }

        [Fact]
        public async Task MissingLocalizationResources_ReturnsNotFoundProblemDetails()
        {
            await using var app = await AHost(new LocalizationResourceResult(
                LocalizationResourceStatus.NotFound, Message: "Default localization resources are unavailable."));

            var (response, problem) = await Get(app, "/api/localization/en-US/common");

            AssertProblemDetails(response, problem, HttpStatusCode.NotFound);
            Assert.Equal("Default localization resources are unavailable.", problem["detail"]!.GetValue<string>());
        }

        [Fact]
        public async Task ServerError_ReturnsGenericDetailWithTraceId()
        {
            await using var app = await AHost(new LocalizationResourceResult(
                LocalizationResourceStatus.MalformedJson, Message: "Localization JSON payload is malformed."));

            var (response, problem) = await Get(app, "/api/localization/en-US/common");

            AssertProblemDetails(response, problem, HttpStatusCode.InternalServerError);
            Assert.Equal("An error occurred in our API. Please use the trace id when requesting assistance.",
                problem["detail"]!.GetValue<string>());
        }

        [Fact]
        public async Task BodilessErrorResponse_IsFilledWithProblemDetails()
        {
            await using var app = await AHost(new LocalizationResourceResult(LocalizationResourceStatus.Ok));

            var (response, problem) = await Get(app, "/api/nhsn-app-bff/no-such-route");

            AssertProblemDetails(response, problem, HttpStatusCode.NotFound);
        }
    }
}
