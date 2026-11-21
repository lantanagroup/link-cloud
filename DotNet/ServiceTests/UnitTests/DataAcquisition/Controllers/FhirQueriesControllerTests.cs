using DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Link.Authorization.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text.Encodings.Web;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class FhirQueriesControllerTests
{
    [Fact]
    public async Task GetQueries_NullOptionalParameters_UnknownFacility_ReturnsNotFound()
    {
        await using var factory = new FhirQueriesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/data-acquisition/facility-unknown/FhirQueries");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetQueries_NullOptionalParameters_ExistingQueries_ReturnsOk()
    {
        await using var factory = new FhirQueriesApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        db.FhirQueries.Add(new FhirQuery
        {
            Id = Guid.NewGuid(),
            FacilityId = "facility-1",
            QueryType = FhirQueryType.Search,
            QueryParameters = new List<string> { "patient=patient-1" },
            DataAcquisitionLogId = 11
        });
        await db.SaveChangesAsync();

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/data-acquisition/facility-1/FhirQueries");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("facility-1", body);
    }

    [Fact]
    public async Task GetQueries_EmptyFacilityId_ReturnsBadRequest()
    {
        var controller = CreateController(new FhirQueryResultModel { Queries = new List<FhirQuery>() });

        var result = await controller.GetQueries("  ", null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetQueries_QueryFailure_WithNullCorrelationId_PreservesOriginalException()
    {
        var queries = new Mock<IFhirQueryQueries>();
        queries
            .Setup(q => q.GetFhirQueriesAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("query failed"));

        var controller = new FhirQueriesController(
            Mock.Of<ILogger<FhirQueriesController>>(),
            Mock.Of<IFhirQueryManager>(),
            queries.Object);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.GetQueries("facility-1", null, null, null, CancellationToken.None));

        Assert.Equal("query failed", thrown.Message);
    }

    private static FhirQueriesController CreateController(FhirQueryResultModel result)
    {
        var queries = new Mock<IFhirQueryQueries>();
        queries
            .Setup(q => q.GetFhirQueriesAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return new FhirQueriesController(
            Mock.Of<ILogger<FhirQueriesController>>(),
            Mock.Of<IFhirQueryManager>(),
            queries.Object);
    }

    private sealed class FhirQueriesApiFactory : WebApplicationFactory<FhirQueriesApiFactory.Marker>
    {
        private readonly string _databaseName = $"FhirQueries_{Guid.NewGuid():N}";

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
                        .ConfigureServices(services =>
                        {
                            services.AddDbContext<DataAcquisitionDbContext>(options =>
                                options.UseInMemoryDatabase(_databaseName));
                            services.AddScoped<IFhirQueryQueries, FhirQueryQueries>();
                            services.AddSingleton(Mock.Of<IFhirQueryManager>());
                            services.AddControllers()
                                .AddApplicationPart(typeof(FhirQueriesController).Assembly);
                            services.AddAuthentication(FhirQueriesTestAuthHandler.SchemeName)
                                .AddScheme<AuthenticationSchemeOptions, FhirQueriesTestAuthHandler>(
                                    FhirQueriesTestAuthHandler.SchemeName, _ => { });
                            services.AddAuthorization(options =>
                                options.AddPolicy(PolicyNames.IsLinkAdmin, policy => policy.RequireAuthenticatedUser()));
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

        public sealed class Marker;
    }

    private sealed class FhirQueriesTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";

        public FhirQueriesTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "unit-test-user") },
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
