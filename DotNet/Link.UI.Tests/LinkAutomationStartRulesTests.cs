using Automation.UI.Models;
using Automation.UI.Services;
using LantanaGroup.Link.Automation.Link.Models;
using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Link.UI.Tests;

public class LinkAutomationStartRulesTests
{
    [Fact]
    public void Built_in_kind_maps_onto_the_engine_request()
    {
        LinkAutomationStartRules.TryParseChoice(
                "kind:AdhocReportTest",
                out var kind,
                out var scenarioId,
                out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        scenarioId.Should().Be(Guid.Empty);
        kind.Should().Be(AutomationScenarioKind.AdhocReportTest);

        LinkAutomationStartRules.TryRunName("  Morning check  ", out var name, out var nameError).Should().BeTrue();
        nameError.Should().BeNull();

        var request = LinkAutomationStartRules.BuildKindRequest(kind!.Value, name);
        request.Scenario.Should().Be(AutomationScenarioKind.AdhocReportTest);
        request.ReportMethod.Should().Be(ReportMethod.Adhoc);
        request.ScenarioName.Should().Be("Morning check");
    }

    [Fact]
    public void Saved_scenario_choice_keeps_the_id()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");

        LinkAutomationStartRules.TryParseChoice(
                LinkAutomationStartRules.ScenarioChoiceValue(id),
                out var kind,
                out var scenarioId,
                out var error)
            .Should().BeTrue();

        kind.Should().BeNull();
        scenarioId.Should().Be(id);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("", "Select a run.")]
    [InlineData("kind:Custom", "That run type is not supported.")]
    [InlineData("kind:NotAKind", "That run type is not supported.")]
    [InlineData("scenario:not-a-guid", "Scenario id is not a valid id.")]
    [InlineData("scenario:00000000-0000-0000-0000-000000000000", "Scenario id is not a valid id.")]
    public void Invalid_choices_stay_on_the_form(string choice, string expected)
    {
        LinkAutomationStartRules.TryParseChoice(choice, out _, out _, out var error).Should().BeFalse();
        error.Should().Be(expected);
    }

    [Fact]
    public void A_long_name_is_rejected_and_markup_is_stripped()
    {
        LinkAutomationStartRules.TryRunName(new string('a', 121), out _, out var error).Should().BeFalse();
        error.Should().Contain("120");

        LinkAutomationStartRules.TryRunName("<b>Ward</b>", out var name, out var markupError).Should().BeTrue();
        markupError.Should().BeNull();
        name.Should().Be("Ward");
    }

    [Fact]
    public void Scenario_labels_sort_custom_ahead_of_system()
    {
        var custom = new AutomationScenarioChoice { Id = Guid.NewGuid(), Name = "Zebra", IsSystemScenario = false };
        var system = new AutomationScenarioChoice { Id = Guid.NewGuid(), Name = "Alpha", IsSystemScenario = true };

        var ordered = LinkAutomationStartRules.OrderScenarios([system, custom]);

        ordered.Select(item => item.Name).Should().Equal("Zebra", "Alpha");
        LinkAutomationStartRules.ScenarioLabel(system).Should().Be("Alpha (System)");
    }

    [Fact]
    public void A_start_failure_uses_the_engine_message()
    {
        LinkAutomationStartRules.ExplainStartFailure(new InvalidOperationException("Facility template 'x' was not found."))
            .Should().Be("Facility template 'x' was not found.");
        LinkAutomationStartRules.ExplainStartFailure(new IOException("disk"))
            .Should().Be("The run could not be started.");
    }

    [Fact]
    public void Engine_stays_off_when_storage_settings_are_missing_or_rejected()
    {
        var missing = new ServiceCollection();
        var missingStatus = LinkAutomationEngine.Add(missing, new ConfigurationBuilder().Build());
        missingStatus.Ready.Should().BeFalse();
        missingStatus.Message.Should().Be(AutomationRunReader.NotConfiguredMessage);
        missing.Any(descriptor => descriptor.ServiceType == typeof(IAutomationRunManager)).Should().BeFalse();

        var rejected = new ServiceCollection();
        var rejectedStatus = LinkAutomationEngine.Add(rejected, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoDB:ConnectionString"] = "not a connection string",
            ["MongoDB:DatabaseName"] = "botw-automation",
            ["InternalBlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
            ["InternalBlobStorage:BlobContainerName"] = "link-automation-ui",
            ["KafkaConnection:BootstrapServers:0"] = "localhost:9094",
            ["Loki:Url"] = "http://localhost:3100",
            ["Loki:App"] = "link-cloud",
            ["PipelineAbort:AllowInMemory"] = "true"
        }).Build());

        rejectedStatus.Ready.Should().BeFalse();
        rejectedStatus.Message.Should().Be(AutomationRunReader.SettingsRejectedMessage);
        rejected.Any(descriptor => descriptor.ServiceType == typeof(IAutomationRunManager)).Should().BeFalse();
    }
}
