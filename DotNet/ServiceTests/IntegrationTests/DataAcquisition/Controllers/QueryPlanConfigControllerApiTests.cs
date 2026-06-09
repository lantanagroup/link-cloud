using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.Shared.Application.Models;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers
{
    /// <summary>
    /// HTTP-pipeline tests for <see cref="QueryPlanConfigController"/>.
    ///
    /// Unlike the direct-call unit tests (which <c>new</c> the controller and seed
    /// ModelState by hand), these go through a real MVC pipeline via
    /// <see cref="WebApplicationFactory{T}"/>: model binding, the <c>[ApiController]</c>
    /// automatic ModelState validation filter, routing, and authorization all run.
    /// That lets us assert what the framework actually does *before* the action body
    /// is reached — which a direct call can never exercise.
    ///
    /// The factory stands up only this controller with mocked dependencies; it does
    /// NOT boot the real <c>Program</c> (which eagerly needs SQL Server, Redis and
    /// Kafka), so the tests stay fast and require no Docker.
    /// </summary>
    [Trait("Category", "IntegrationTests")]
    public class QueryPlanConfigControllerApiTests : IClassFixture<QueryPlanConfigApiFactory>
    {
        private readonly QueryPlanConfigApiFactory _factory;

        public QueryPlanConfigControllerApiTests(QueryPlanConfigApiFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        [Fact]
        public async Task GetQueryPlan_WhitespaceFacilityId_RejectedByFrameworkBeforeAction()
        {
            // "%20" decodes to a single space. It matches the route (non-empty
            // segment), but [Required] trims whitespace by default, so the
            // [ApiController] filter 400s it *before* the action runs. The body is the
            // framework's default ValidationProblemDetails ("errors" shape), NOT the
            // action's invalid-params output.
            _factory.QueryPlanQueries.Reset();
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/data/%20/QueryPlan?type=Monthly");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("\"errors\"", body);
            Assert.Contains("The facilityId field is required.", body);
        }

        [Fact]
        public async Task GetQueryPlan_HtmlStrippedFacilityId_ReachesActionAndReturnsInvalidParams()
        {
            // "%40%40%40" decodes to "@@@": non-whitespace, so it passes [Required]
            // and reaches the action. There SanitizeAndRemove() strips every
            // disallowed char, leaving an empty string, and the in-action guard
            // returns InvalidParametersProblem. The body is the action's RFC 9457
            // invalid-params shape -- proof the request reached the controller method.
            _factory.QueryPlanQueries.Reset();
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/data/%40%40%40/QueryPlan?type=Monthly");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("rfc9457#section-3", body);
            Assert.Contains("parameter facilityId is required.", body);
            // The action short-circuits at the guard, so the data layer is untouched.
            _factory.QueryPlanQueries.Verify(
                x => x.GetAsync(It.IsAny<string>(), It.IsAny<Frequency>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetQueryPlan_MissingType_FrameworkReturns400_AndActionNeverRuns()
        {
            // Type is [Required] on GetQueryPlanParameters, so the [ApiController]
            // filter rejects the request before the action body executes.
            _factory.QueryPlanQueries.Reset();
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/data/test-facility/QueryPlan");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // Proof the action was short-circuited: the data layer was never touched.
            _factory.QueryPlanQueries.Verify(
                x => x.GetAsync(It.IsAny<string>(), It.IsAny<Frequency>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetQueryPlan_InvalidTypeValue_FrameworkReturns400()
        {
            // An unparseable enum value fails model binding -> invalid ModelState -> 400.
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/data/test-facility/QueryPlan?type=NotAFrequency");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetQueryPlan_EmptyFacilityId_RouteReturns404_NotBadRequest()
        {
            // The route template is api/data/{facilityId}/QueryPlan. An empty path
            // segment does not match {facilityId}, so the real pipeline returns 404
            // here -- NOT the 400 that the direct-call unit test asserts. The unit
            // test can't surface this difference because it bypasses routing.
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/data//QueryPlan?type=Monthly");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetQueryPlan_ValidRequest_PassesValidationAndReachesAction()
        {
            _factory.QueryPlanQueries.Reset();
            _factory.QueryPlanQueries
                .Setup(x => x.GetAsync("facility-1", Frequency.Monthly, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryPlanModel { FacilityId = "facility-1", Type = Frequency.Monthly });

            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/data/facility-1/QueryPlan?type=Monthly");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _factory.QueryPlanQueries.Verify(
                x => x.GetAsync("facility-1", Frequency.Monthly, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateQueryPlan_NullBody_FrameworkReturns400_AndActionNeverRuns()
        {
            // [Required, FromBody] QueryPlanApiModel with no request body: the body
            // model binder produces a validation error, so the [ApiController] filter
            // 400s before the action runs.
            _factory.QueryPlanManager.Reset();
            var client = _factory.CreateClient();

            var response = await client.PostAsync("/api/data/test-facility/QueryPlan", content: null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _factory.QueryPlanManager.Verify(
                x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateQueryPlan_EmptyBody_FrameworkReturns400_AndActionNeverRuns()
        {
            // An empty JSON payload is also rejected before the action: the body fails
            // to bind / [Required] is unsatisfied.
            _factory.QueryPlanManager.Reset();
            var client = _factory.CreateClient();

            var response = await client.PostAsync(
                "/api/data/test-facility/QueryPlan",
                new StringContent("", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _factory.QueryPlanManager.Verify(
                x => x.AddAsync(It.IsAny<CreateQueryPlanModel>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateQueryPlan_NullBody_FrameworkReturns400_AndActionNeverRuns()
        {
            _factory.QueryPlanManager.Reset();
            var client = _factory.CreateClient();

            var response = await client.PutAsync("/api/data/test-facility/QueryPlan", content: null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _factory.QueryPlanManager.Verify(
                x => x.UpdateAsync(It.IsAny<UpdateQueryPlanModel>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateQueryPlan_EmptyBody_FrameworkReturns400_AndActionNeverRuns()
        {
            _factory.QueryPlanManager.Reset();
            var client = _factory.CreateClient();

            var response = await client.PutAsync(
                "/api/data/test-facility/QueryPlan",
                new StringContent("", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _factory.QueryPlanManager.Verify(
                x => x.UpdateAsync(It.IsAny<UpdateQueryPlanModel>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    /// <summary>
    /// A self-contained <see cref="WebApplicationFactory{T}"/> that hosts only
    /// <see cref="QueryPlanConfigController"/> through a real MVC pipeline with mocked
    /// dependencies and a permissive test authentication scheme.
    /// </summary>
    public sealed class QueryPlanConfigApiFactory : WebApplicationFactory<QueryPlanConfigApiFactory.ApiTestMarker>
    {
        public Mock<IQueryPlanManager> QueryPlanManager { get; } = new();
        public Mock<IQueryPlanQueries> QueryPlanQueries { get; } = new();

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // WAF defaults the content root to a path derived from the test assembly
            // name (which doesn't exist on disk). Pin it to the test output dir.
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
                        .ConfigureServices(services =>
                        {
                            services
                                .AddControllers(options =>
                                    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
                                .AddApplicationPart(typeof(QueryPlanConfigController).Assembly)
                                .AddJsonOptions(o =>
                                {
                                    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                                    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                                    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                                    o.JsonSerializerOptions.Converters.Add(new QueryConfigConverter());
                                    o.JsonSerializerOptions.Converters.Add(new ParameterConverter());
                                    o.JsonSerializerOptions.Converters.Add(new TimeSpanConverter());
                                });

                            // The controller's collaborators are mocked: these tests are
                            // about the pipeline, not data access.
                            services.AddSingleton(QueryPlanManager.Object);
                            services.AddSingleton(QueryPlanQueries.Object);

                            // Satisfy [Authorize(Policy = IsLinkAdmin)] with a test scheme
                            // that always authenticates. The policy still requires an
                            // authenticated user, so the authz path is genuinely exercised.
                            services
                                .AddAuthentication(TestAuthHandler.SchemeName)
                                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                    TestAuthHandler.SchemeName, _ => { });
                            services.AddAuthorization(options =>
                                options.AddPolicy(PolicyNames.IsLinkAdmin, p => p.RequireAuthenticatedUser()));
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseAuthentication();
                            app.UseAuthorization();
                            app.UseEndpoints(endpoints => endpoints.MapControllers());
                        });
                });
        }

        /// <summary>Marker type used only to locate the test assembly/content root.</summary>
        public sealed class ApiTestMarker { }
    }

    /// <summary>
    /// Authentication handler that authenticates every request, so authorization
    /// policies that only require an authenticated user pass in tests.
    /// </summary>
    public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, "integration-test-user") },
                SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
