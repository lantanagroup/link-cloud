using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class FacilityConfigurationRoutingTests
{
    [Fact]
    public void Facility_pages_and_the_run_engine_use_one_configuration_service()
    {
        var service = File.ReadAllText(RepoFile("DotNet/Automation.Link/Helpers/FacilityConfigurationService.cs"));
        var sections = File.ReadAllText(RepoFile("DotNet/Automation.Link/Helpers/FacilityConfigurationService.Sections.cs"));
        var helper = File.ReadAllText(RepoFile("DotNet/Automation.Link/Helpers/FacilitySetupHelper.cs"));
        var hub = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/FacilityHubService.cs"));
        var acquisition = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/FacilityAcquisitionService.cs"));
        var normalization = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/FacilityNormalizationService.cs"));
        var configuration = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/ConfigurationService.cs"));
        var executor = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/RunExecutor.cs"));

        service.Should().Contain("EnsureFacilityAsync");
        service.Should().Contain("EnsureEmptyDmrpFacilityAsync");
        sections.Should().Contain("CreateFacilityAsync");
        sections.Should().Contain("SaveCensusAsync");
        sections.Should().Contain("SaveNotificationConfigurationAsync");

        helper.Should().Contain("FacilityConfigurationService.EnsureFacilityAsync");
        helper.Should().Contain("FacilityConfigurationService.CleanupFacilityAsync");
        helper.Should().NotContain("new FacilityModel");
        helper.Should().NotContain("facilityClient.CreateAsync");

        hub.Should().Contain("FacilityConfigurationService.CreateFacilityAsync");
        hub.Should().Contain("FacilityConfigurationService.SaveCensusAsync");
        hub.Should().Contain("FacilityConfigurationService.SaveQueryDispatchAsync");
        hub.Should().NotContain("_facilities.CreateAsync");
        hub.Should().NotContain("_facilities.UpdateAsync");
        hub.Should().NotContain("_census.CreateCensusConfigAsync");
        hub.Should().NotContain("_queryDispatch.UpsertQueryDispatchConfigurationAsync");

        acquisition.Should().Contain("FacilityConfigurationService.SaveFhirQueryAsync");
        acquisition.Should().Contain("FacilityConfigurationService.CreateQueryPlanAsync");
        acquisition.Should().NotContain("_client.CreateFhirQueryConfigurationAsync");
        acquisition.Should().NotContain("_client.CreateQueryPlanAsync");

        normalization.Should().Contain("FacilityConfigurationService.CreateNormalizationOperationAsync");
        normalization.Should().NotContain("_client.CreateOperationAsync");

        configuration.Should().Contain("FacilityConfigurationService.SaveNotificationConfigurationAsync");
        configuration.Should().NotContain("_notification.CreateConfigurationAsync");

        executor.Should().Contain("FacilitySetupHelper.EnsureFacilityAsync");
        executor.Should().Contain("FacilitySetupHelper.EnsureEmptyDmrpFacilityAsync");
        executor.Should().Contain("state.AutomationCreatedFacility = await FacilitySetupHelper.EnsureFacilityAsync");
        executor.Should().Contain("FacilityConfigurationService.CreateNormalizationOperationAsync");
        var owns = executor.IndexOf("FacilityClassification.RunOwns", StringComparison.Ordinal);
        var cleanup = executor.IndexOf("RunCleanupHelper.CleanupAfterRunAsync", StringComparison.Ordinal);
        owns.Should().BeGreaterThan(0);
        cleanup.Should().BeGreaterThan(owns);
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
