using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using FluentAssertions;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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

        results.Should().ContainSingle();
        var result = results.Single();
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Be("ServiceRegistry:AdminBffServiceUrl is not configured.");
        result.RequestBody.Should().Contain("ServiceRegistry:AdminBffServiceUrl is missing");
        result.ResponseBody.Should().Contain("ServiceRegistry:AdminBffServiceUrl is missing");

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
}
