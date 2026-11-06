using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class SuggestedWorkaroundBuilderTests
{
    [Fact]
    public void Confirmed_org_mapping_exclusion_lists_counts_map_and_steps()
    {
        var workaround = SuggestedWorkaroundBuilder.Match(Input(
            AutomationRunStatus.Failed,
            Summary(encounters: 12, mapped: 0, orgLocations: 0, locationMappings: 4, activeConfigurations: 1),
            "Epic",
            Map("Epic", "identifier.where(system='urn:oid:1.2.840.114350').exists()")));

        workaround.Should().NotBeNull();
        workaround!.RuleId.Should().Be(SuggestedWorkaroundBuilder.OrgMappingExcludedAllRuleId);
        workaround.Title.Should().Be("Suggested workaround");
        workaround.Cause.Should().Contain("excluded every encounter");
        workaround.OrganizationMapName.Should().Be("Epic");
        workaround.Counts.Should().Contain(count => count.Label == "Encounters found" && count.Value == 12);
        workaround.Counts.Should().Contain(count => count.Label == "Excluded by organization mapping" && count.Value == 12);
        workaround.Counts.Should().Contain(count => count.Label == "Mapped to organization" && count.Value == 0);
        workaround.Counts.Should().Contain(count => count.Label == "Organization locations" && count.Value == 0);
        workaround.Conditions.Should().ContainSingle(condition =>
            condition.Priority == 1
            && condition.FhirPath == "identifier.where(system='urn:oid:1.2.840.114350').exists()");
        workaround.Steps.Should().HaveCount(2);
        workaround.Steps.Should().Contain(step => step.Contains("does not change the map or start a run"));
        workaround.OrganizationMapsUrl.Should().Be("/OrganizationResourceMaps");
    }

    [Fact]
    public void Partial_exclusion_does_not_trigger()
    {
        var workaround = SuggestedWorkaroundBuilder.Match(Input(
            AutomationRunStatus.Failed,
            Summary(encounters: 12, mapped: 3, orgLocations: 1, locationMappings: 4, activeConfigurations: 1)));

        workaround.Should().BeNull();
    }

    [Fact]
    public void Missing_org_location_data_does_not_trigger()
    {
        SuggestedWorkaroundBuilder.Match(Input(AutomationRunStatus.Failed, summary: null)).Should().BeNull();
        SuggestedWorkaroundBuilder.Match(Input(AutomationRunStatus.Failed, Summary(encounters: 0, mapped: 0, orgLocations: 0, locationMappings: 0, activeConfigurations: 1))).Should().BeNull();
        SuggestedWorkaroundBuilder.Match(Input(AutomationRunStatus.Failed, Summary(encounters: 4, mapped: 0, orgLocations: 0, locationMappings: 1, activeConfigurations: 0))).Should().BeNull();
    }

    [Fact]
    public void Succeeded_run_does_not_trigger_when_the_counts_match()
    {
        var workaround = SuggestedWorkaroundBuilder.Match(Input(
            AutomationRunStatus.Succeeded,
            Summary(encounters: 4, mapped: 0, orgLocations: 0, locationMappings: 1, activeConfigurations: 1)));

        workaround.Should().BeNull();
    }

    [Fact]
    public void Confirmed_case_keeps_the_applied_map_name_when_the_template_is_gone()
    {
        var workaround = SuggestedWorkaroundBuilder.Match(Input(
            AutomationRunStatus.Failed,
            Summary(encounters: 2, mapped: 0, orgLocations: 1, locationMappings: 1, activeConfigurations: 1),
            appliedMapName: "Epic"));

        workaround.Should().NotBeNull();
        workaround!.OrganizationMapName.Should().Be("Epic");
        workaround.Conditions.Should().BeEmpty();
        workaround.ConditionsNote.Should().Contain("not available");
        workaround.Counts.Should().Contain(count => count.Label == "Organization locations" && count.Value == 1);
    }

    [Fact]
    public void Legacy_row_snapshot_rolls_up_to_the_same_confirmation()
    {
        var summary = SuggestedWorkaroundBuilder.ResolveOrgLocation(
            stored: new OrgLocationSummary(),
            legacy: new StoreBackedServicePoller.OrgLocationSnapshot(
                [new PipelineDataReader.OrganizationLocationConfigurationInfo(1, true, 1)],
                [new PipelineDataReader.OrganizationLocationMappingInfo("facility", "loc-1", false, true, null)],
                [
                    new PipelineDataReader.EncounterMappingInfo(
                        "facility",
                        "patient-1",
                        "enc-1",
                        false,
                        [new PipelineDataReader.EncounterLocationInfo("loc-1")])
                ]));

        var workaround = SuggestedWorkaroundBuilder.Match(Input(AutomationRunStatus.Failed, summary));

        workaround.Should().NotBeNull();
        workaround!.Counts.Should().Contain(count => count.Label == "Encounters found" && count.Value == 1);
        workaround.Counts.Should().Contain(count => count.Label == "Excluded by organization mapping" && count.Value == 1);
        workaround.Counts.Should().Contain(count => count.Label == "Mapped to organization" && count.Value == 0);
    }

    private static SuggestedWorkaroundInput Input(
        AutomationRunStatus status,
        OrgLocationSummary? summary,
        string? appliedMapName = null,
        OrganizationResourceMapTemplate? map = null)
        => new()
        {
            Status = status,
            Summary = summary,
            AppliedMapName = appliedMapName,
            Map = map,
            OrganizationMapsUrl = "/OrganizationResourceMaps"
        };

    private static OrgLocationSummary Summary(
        int encounters,
        int mapped,
        int orgLocations,
        int locationMappings,
        int activeConfigurations)
        => new()
        {
            ConfigurationCount = activeConfigurations,
            ActiveConfigurationCount = activeConfigurations,
            LocationMappingCount = locationMappings,
            OrgLocationMappingCount = orgLocations,
            EncounterMappingCount = encounters,
            MappedToOrgCount = mapped
        };

    private static OrganizationResourceMapTemplate Map(string name, string fhirPath)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Conditions = [new OrganizationResourceMapCondition { Priority = 1, FhirPath = fhirPath }]
        };
}
