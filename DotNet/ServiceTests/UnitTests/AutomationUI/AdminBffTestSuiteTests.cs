using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using UnitTests.Admin.BFF.Aggregation;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AdminBffTestSuiteTests
{
    [Fact]
    public async Task ExecuteAsync_returns_missing_configuration_diagnostic_when_AdminBffServiceUrl_is_not_configured()
    {
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        var serviceRegistry = Options.Create(new ServiceRegistry());
        var seedContext = new Mock<IApiHealthSeedContextAccessor>();
        var logger = new Mock<ILogger<AdminBffTestSuite>>();

        var suite = new AdminBffTestSuite(
            serviceProvider.Object,
            serviceRegistry,
            seedContext.Object,
            logger.Object);

        var results = await suite.ExecuteAsync();

        results.Should().HaveCount(2);

        results.Should().OnlyContain(result =>
            !result.Passed &&
            result.ErrorMessage == "ServiceRegistry:AdminBffServiceUrl is not configured." &&
            result.RequestBody.Contains("Admin BFF service URL is missing") &&
            result.ResponseBody.Contains("Admin BFF service URL is missing"));

        results.Select(r => r.EndpointName).Should().BeEquivalentTo(
        [
            ApiEndPointLibrary.AdminBffSteps.InfoGet200,
            ApiEndPointLibrary.AdminBffSteps.HealthGet200
        ]);

        serviceProvider.Verify(sp => sp.GetService(typeof(LantanaGroup.Link.Sdk.Clients.IAdminBffIntegrationClient)), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_rethrows_cancellation_before_invoking_AdminBff_client_calls()
    {
        var adminBffClient = new Mock<IAdminBffIntegrationClient>(MockBehavior.Strict);
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IAdminBffIntegrationClient)))
            .Returns(adminBffClient.Object);

        var serviceRegistry = Options.Create(new ServiceRegistry
        {
            AdminBffServiceUrl = "http://localhost:8063"
        });
        var seedContext = new Mock<IApiHealthSeedContextAccessor>();
        var logger = new Mock<ILogger<AdminBffTestSuite>>();

        var suite = new AdminBffTestSuite(
            serviceProvider.Object,
            serviceRegistry,
            seedContext.Object,
            logger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await FluentActions
            .Invoking(() => suite.ExecuteAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();

        adminBffClient.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("http://host:8063")]
    [InlineData("http://host:8063/api")]
    public async Task ExecuteAsync_returns_normalized_facility_diagnostics_when_creation_fails(
    string adminBffServiceUrl)
    {
        // Arrange
        var adminBffClient = new Mock<IAdminBffIntegrationClient>(MockBehavior.Strict);

        adminBffClient
            .Setup(x => x.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 200,
                RequestUrl = "http://host:8063/api/monitor/health"
            });

        string? createdFacilityId = null;

        adminBffClient
            .Setup(x => x.CreateFacilityAsync(
                It.IsAny<FacilityModel>(),
                It.IsAny<CancellationToken>()))
            .Callback<FacilityModel, CancellationToken>((facility, _) =>
                createdFacilityId = facility.FacilityId)
            .ReturnsAsync(new LinkApiResponse<FacilityModel>
            {
                StatusCode = 500
            });

        // These calls occur after the failed facility prerequisite.
        adminBffClient
            .Setup(x => x.SoftDeleteAggregateFacilityAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse
            {
                StatusCode = 404,
                RequestUrl = "http://host:8063/api/aggregate/facility/missing"
            });

        adminBffClient
            .Setup(x => x.RestoreAggregateFacilityAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse
            {
                StatusCode = 404,
                RequestUrl = "http://host:8063/api/aggregate/facility/missing/restore"
            });

        adminBffClient
            .Setup(x => x.GetReportSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 200,
                RequestUrl = "http://host:8063/api/aggregate/reports/summaries"
            });

        adminBffClient
            .Setup(x => x.GetReportSummaryAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<string>
            {
                StatusCode = 404,
                RequestUrl = "http://host:8063/api/aggregate/reports/summaries/missing"
            });

        adminBffClient
            .Setup(x => x.DeleteAggregateReportAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse
            {
                StatusCode = 404,
                RequestUrl = "http://host:8063/api/aggregate/reports/missing"
            });

        adminBffClient
            .Setup(x => x.RestoreAggregateReportAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse
            {
                StatusCode = 404,
                RequestUrl = "http://host:8063/api/aggregate/reports/missing/restore"
            });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient("ApiHealthTest"))
            .Returns(() => new HttpClient(new MockHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })));

        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);

        serviceProvider
            .Setup(sp => sp.GetService(typeof(IHttpClientFactory)))
            .Returns(httpClientFactory.Object);

        serviceProvider
            .Setup(sp => sp.GetService(typeof(IAdminBffIntegrationClient)))
            .Returns(adminBffClient.Object);

        var serviceRegistry = Options.Create(new ServiceRegistry
        {
            AdminBffServiceUrl = adminBffServiceUrl
        });

        var seedContext = new Mock<IApiHealthSeedContextAccessor>();
        var logger = new Mock<ILogger<AdminBffTestSuite>>();

        var suite = new AdminBffTestSuite(
            serviceProvider.Object,
            serviceRegistry,
            seedContext.Object,
            logger.Object);

        // Act
        var results = await suite.ExecuteAsync();

        // Assert
        createdFacilityId.Should().NotBeNullOrWhiteSpace();

        var expectedFacilityUrl =
            $"http://host:8063/api/aggregate/facility/{createdFacilityId}";

        var deleteResult = results.Single(
            r => r.EndpointName == ApiEndPointLibrary.AdminBffSteps.FacilityDelete200);

        deleteResult.Passed.Should().BeFalse();
        deleteResult.RequestMethod.Should().Be("DELETE");
        deleteResult.ExpectedStatusCode.Should().Be(200);
        deleteResult.ActualStatusCode.Should().BeNull();
        deleteResult.RequestUrl.Should().Be(expectedFacilityUrl);

        var restoreResult = results.Single(
            r => r.EndpointName == ApiEndPointLibrary.AdminBffSteps.FacilityRestorePatch200);

        restoreResult.Passed.Should().BeFalse();
        restoreResult.RequestMethod.Should().Be("PATCH");
        restoreResult.ExpectedStatusCode.Should().Be(200);
        restoreResult.ActualStatusCode.Should().BeNull();
        restoreResult.RequestUrl.Should().Be($"{expectedFacilityUrl}/restore");

        var reportDeleteResult = results.Single(
            r => r.EndpointName == ApiEndPointLibrary.AdminBffSteps.ReportDelete204);

        reportDeleteResult.Passed.Should().BeFalse();
        reportDeleteResult.RequestMethod.Should().Be("DELETE");
        reportDeleteResult.ExpectedStatusCode.Should().Be(204);
        reportDeleteResult.ActualStatusCode.Should().BeNull();
        reportDeleteResult.RequestUrl.Should().Be("http://host:8063/api/aggregate/reports/{reportScheduleId}");

        var reportRestoreResult = results.Single(
            r => r.EndpointName == ApiEndPointLibrary.AdminBffSteps.ReportRestorePatch204);

        reportRestoreResult.Passed.Should().BeFalse();
        reportRestoreResult.RequestMethod.Should().Be("PATCH");
        reportRestoreResult.ExpectedStatusCode.Should().Be(204);
        reportRestoreResult.ActualStatusCode.Should().BeNull();
        reportRestoreResult.RequestUrl.Should().Be("http://host:8063/api/aggregate/reports/{reportScheduleId}/restore");
    }
}
