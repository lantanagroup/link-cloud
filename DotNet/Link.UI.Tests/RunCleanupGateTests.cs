using System.Text.Json;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation;
using LantanaGroup.Automation.Helpers;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class RunCleanupGateTests
{
    [Fact]
    public void A_flag_that_is_on_cleans_only_after_success()
    {
        RunCleanupGate.ShouldCleanup(setting: true, succeeded: true).Should().BeTrue();
        RunCleanupGate.ShouldCleanup(setting: true, succeeded: false).Should().BeFalse();
        RunCleanupGate.ShouldCleanup(setting: false, succeeded: true).Should().BeFalse();
        RunCleanupGate.ShouldCleanup(setting: false, succeeded: false).Should().BeFalse();
    }

    [Fact]
    public void Service_cleanup_requires_success_and_ownership()
    {
        RunCleanupGate.ShouldDeleteServiceData(setting: true, succeeded: true, runOwnsFacility: true).Should().BeTrue();
        RunCleanupGate.ShouldDeleteServiceData(setting: true, succeeded: true, runOwnsFacility: false).Should().BeFalse();
        RunCleanupGate.ShouldDeleteServiceData(setting: true, succeeded: false, runOwnsFacility: true).Should().BeFalse();
        RunCleanupGate.ShouldDeleteServiceData(setting: false, succeeded: true, runOwnsFacility: true).Should().BeFalse();
    }

    [Fact]
    public void An_unowned_facility_is_not_deleted_on_success()
    {
        var runId = Guid.Parse("6d2e1c0b-4a3f-4e8d-9c71-2b0a8f6e5d44");
        var reused = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var owns = FacilityClassification.RunOwns(runId, reused.ToString(), created: false, reused.ToString());

        owns.Should().BeFalse();
        RunCleanupGate.ShouldDeleteServiceData(setting: true, succeeded: true, owns).Should().BeFalse();
        RunCleanupGate.SuccessNotice(removedFhir: true, removedService: false, serviceKeptBecauseUnowned: true)
            .Should().Be("Removed this run's FHIR data. Service data was kept because this run did not create the facility. Clean it up later from Cleanup, which acts on automation facilities only.");
    }

    [Fact]
    public void Failure_cancel_and_abort_keep_data_and_say_so()
    {
        RunCleanupGate.DataKeptNotice(cleanupFhirData: true, cleanupServiceData: true)
            .Should().Be("FHIR data and service data were kept so this run can be investigated. Clean it up later from Cleanup, which acts on automation facilities only.");
        RunCleanupGate.DataKeptNotice(cleanupFhirData: false, cleanupServiceData: true)
            .Should().Be("Service data was kept so this run can be investigated. Clean it up later from Cleanup, which acts on automation facilities only.");
        RunCleanupGate.DataKeptNotice(cleanupFhirData: true, cleanupServiceData: false)
            .Should().Be("FHIR data was kept so this run can be investigated. Clean it up later from Cleanup, which acts on automation facilities only.");
        RunCleanupGate.DataKeptNotice(cleanupFhirData: false, cleanupServiceData: false).Should().BeNull();
    }

    [Fact]
    public void Success_notice_names_only_what_was_removed()
    {
        RunCleanupGate.SuccessNotice(true, true, false).Should().Be("Removed this run's FHIR data and service data.");
        RunCleanupGate.SuccessNotice(false, true, false).Should().Be("Removed this run's service data.");
        RunCleanupGate.SuccessNotice(true, false, false).Should().Be("Removed this run's FHIR data.");
        RunCleanupGate.SuccessNotice(false, false, true)
            .Should().Be("Service data was kept because this run did not create the facility. Clean it up later from Cleanup, which acts on automation facilities only.");
        RunCleanupGate.SuccessNotice(false, false, false).Should().BeNull();
    }

    [Fact]
    public void A_pre_existing_resource_is_not_tracked_for_delete()
    {
        FhirDataLoader.IsCreatedLocationStatus("201 Created").Should().BeTrue();
        FhirDataLoader.IsCreatedLocationStatus("200 OK").Should().BeFalse();
        FhirDataLoader.IsCreatedLocationStatus("200").Should().BeFalse();
        FhirDataLoader.IsCreatedLocationStatus(null).Should().BeFalse();

        var loader = new FhirDataLoader("http://localhost");
        loader.TrackTransactionResponseForCleanup(
            """
            {
              "resourceType": "Bundle",
              "type": "transaction-response",
              "entry": [
                { "response": { "status": "201 Created", "location": "Patient/Patient-runtag-1/_history/1" } },
                { "response": { "status": "200 OK", "location": "Patient/207727/_history/4" } }
              ]
            }
            """,
            new SilentOutput());

        loader.CreatedResourcePaths.Should().Contain("Patient/Patient-runtag-1");
        loader.CreatedResourcePaths.Should().NotContain("Patient/207727");
        loader.CreatedResourcePaths.Should().NotContain(path => path.Contains("207727", StringComparison.Ordinal));
    }

    [Fact]
    public void Seeded_system_scenarios_clean_both_on_success()
    {
        var scenarios = ScenarioSeedService.BuildSystemScenarioList();

        scenarios.Should().HaveCount(10);
        scenarios.Should().OnlyContain(scenario =>
            scenario.IsSystemScenario && scenario.CleanupServiceData && scenario.CleanupFhirData);
        scenarios.Select(scenario => scenario.Id).Should().Contain(new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000008")
        });
    }

    [Fact]
    public void A_user_scenario_keeps_explicit_cleanup_flags_through_the_store()
    {
        var model = new TestScenarioDefinition
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Name = "User scenario",
            CleanupServiceData = false,
            CleanupFhirData = false
        };

        var roundTrip = MongoScenarioStore.RoundTripCleanupFlags(model);

        roundTrip.CleanupServiceData.Should().BeFalse();
        roundTrip.CleanupFhirData.Should().BeFalse();
        roundTrip.IsSystemScenario.Should().BeFalse();

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var missing = JsonSerializer.Deserialize<TestScenarioDefinition>("{}", options);
        missing!.CleanupFhirData.Should().BeTrue();
        missing.CleanupServiceData.Should().BeFalse();

        var explicitFlags = JsonSerializer.Deserialize<TestScenarioDefinition>(
            """{"cleanupFhirData":false,"cleanupServiceData":true}""",
            options);
        explicitFlags!.CleanupFhirData.Should().BeFalse();
        explicitFlags.CleanupServiceData.Should().BeTrue();
    }

    [Fact]
    public void A_stored_run_configuration_uses_the_same_flag_defaults()
    {
        RunCleanupGate.DataKeptNotice("""{"cleanupFhirData":false,"cleanupServiceData":false}""").Should().BeNull();
        RunCleanupGate.DataKeptNotice("{}")!
            .Should().Contain("FHIR data was kept");
        RunCleanupGate.DataKeptNotice("not json").Should().BeNull();
    }

    [Fact]
    public void The_run_engine_asks_the_gate_before_it_deletes()
    {
        var executor = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/RunExecutor.cs"));
        executor.Should().Contain("RunCleanupGate.ShouldCleanup");
        executor.Should().Contain("RunCleanupGate.ShouldDeleteServiceData");
        executor.Should().Contain("FacilityClassification.RunOwns");
        executor.Should().Contain("RunCleanupGate.DataKeptNotice");
        executor.Should().Contain("runSucceeded: true");

        var helper = File.ReadAllText(RepoFile("DotNet/Automation.Link/Helpers/RunCleanupHelper.cs"));
        helper.Should().Contain("if (!runSucceeded)");
        var cancelStart = helper.IndexOf("Task CleanupCancelledRunAsync", StringComparison.Ordinal);
        var cancelEnd = helper.IndexOf("Task CleanupLeftoverFacilityAsync", StringComparison.Ordinal);
        cancelStart.Should().BeGreaterThan(0);
        cancelEnd.Should().BeGreaterThan(cancelStart);
        var cancelBody = helper[cancelStart..cancelEnd];
        cancelBody.Should().NotContain("DeleteResourcesWithExpunge");
        cancelBody.Should().Contain("deactivateSchedules: false");

        var orchestrator = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/ApiHealth/Seeding/ApiHealthSeedOrchestrator.cs"));
        orchestrator.Should().Contain("startRequest.CleanupServiceData = false");
        orchestrator.Should().Contain("FacilityClassification.RunOwns");

        var health = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/ApiHealth/ApiHealthExecutionRunManager.cs"));
        health.Should().Contain("Seed facility kept because this API Health run did not succeed.");

        var editor = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_ScenarioEditorModal.cshtml"));
        editor.Should().Contain("only when the run succeeds");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class SilentOutput : IAutomationOutput
    {
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
