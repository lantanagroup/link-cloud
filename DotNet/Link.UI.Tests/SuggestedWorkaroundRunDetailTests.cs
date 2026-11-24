using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// A failed run shows the suggested workaround under its error when the stored organization-location
/// counts confirm one. The panel reads the same builder the diagnostics export uses.
/// </summary>
public class SuggestedWorkaroundRunDetailTests
{
    [Fact]
    public void Confirmed_exclusion_keeps_the_applied_map_and_the_page_link()
    {
        var summary = new OrgLocationSummary
        {
            ConfigurationCount = 1,
            ActiveConfigurationCount = 1,
            EncounterMappingCount = 12,
            MappedToOrgCount = 0,
            LocationMappingCount = 4,
            OrgLocationMappingCount = 0
        };
        var map = new OrganizationResourceMapTemplate
        {
            Name = "Epic",
            Conditions =
            [
                new OrganizationResourceMapCondition
                {
                    Priority = 1,
                    FhirPath = "identifier.exists()"
                }
            ]
        };

        var workaround = AutomationRunDetailFiller.MatchStored(
            new AutomationRunSummary { Status = AutomationRunStatus.Failed },
            summary,
            legacy: null,
            new GenerationManifestSnapshot { AppliedOrganizationResourceMapName = "Epic" },
            map,
            "/Automation/OrganizationResourceMaps/Index");

        workaround.Should().NotBeNull();
        workaround!.RuleId.Should().Be(SuggestedWorkaroundBuilder.OrgMappingExcludedAllRuleId);
        workaround.Cause.Should().Contain("excluded every encounter");
        workaround.OrganizationMapName.Should().Be("Epic");
        workaround.OrganizationMapsUrl.Should().Be("/Automation/OrganizationResourceMaps/Index");
        workaround.Counts.Should().Contain(count => count.Label == "Encounters found" && count.Value == 12);
        workaround.Counts.Should().Contain(count => count.Label == "Excluded by organization mapping" && count.Value == 12);
        workaround.Steps.Should().Contain(step => step.Contains("does not change the map or start a run"));
    }

    [Fact]
    public void A_run_that_is_not_failed_or_only_partly_excluded_has_no_suggestion()
    {
        var summary = new OrgLocationSummary
        {
            ConfigurationCount = 1,
            ActiveConfigurationCount = 1,
            EncounterMappingCount = 12,
            MappedToOrgCount = 0,
            LocationMappingCount = 4
        };

        AutomationRunDetailFiller.MatchStored(
            new AutomationRunSummary { Status = AutomationRunStatus.Succeeded },
            summary,
            legacy: null,
            manifest: null,
            map: null,
            organizationMapsUrl: "/Automation/OrganizationResourceMaps/Index").Should().BeNull();

        summary.MappedToOrgCount = 3;
        AutomationRunDetailFiller.MatchStored(
            new AutomationRunSummary { Status = AutomationRunStatus.Failed },
            summary,
            legacy: null,
            manifest: null,
            map: null,
            organizationMapsUrl: null).Should().BeNull();
    }

    [Fact]
    public void The_manifest_map_wins_and_the_run_configuration_is_the_fallback()
    {
        var manifestId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var configId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var json = $$"""{"organizationResourceMapTemplateId":"{{configId}}"}""";

        AutomationRunDetailFiller.OrganizationMapId(
            new GenerationManifestSnapshot { AppliedOrganizationResourceMapTemplateId = manifestId },
            json).Should().Be(manifestId);
        AutomationRunDetailFiller.OrganizationMapId(null, json).Should().Be(configId);
        AutomationRunDetailFiller.OrganizationMapId(new GenerationManifestSnapshot(), null).Should().BeNull();
    }

    [Fact]
    public void Page_json_is_camel_case_and_does_not_leave_a_raw_less_than()
    {
        var workaround = new SuggestedWorkaround
        {
            Cause = "Organization mapping excluded every encounter.",
            Conditions =
            [
                new SuggestedWorkaroundCondition { Priority = 1, FhirPath = "value < 2" }
            ]
        };

        var json = AutomationRunDetailFiller.SerializeForPage(workaround);

        json.Should().Contain("\"cause\"");
        json.Should().NotContain("<");
        json.ToLowerInvariant().Should().Contain("\\u003c");
        AutomationRunDetailFiller.SerializeForPage(null).Should().Be("null");
    }

    [Fact]
    public void Run_detail_renders_the_panel_from_the_stored_suggestion()
    {
        var detail = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_RunDetail.cshtml"));
        var errorAt = detail.IndexOf("id=\"errorRow\"", StringComparison.Ordinal);
        var panelAt = detail.IndexOf("id=\"suggestedWorkaround\"", StringComparison.Ordinal);
        errorAt.Should().BeGreaterThan(-1);
        panelAt.Should().BeGreaterThan(errorAt);
        detail.Should().Contain("au-help-panel au-workaround-panel");
        detail.Should().Contain("id=\"suggestedWorkaroundBody\"");
        detail.Should().Contain("id=\"suggestedWorkaroundSource\"");
        detail.Should().Contain("ViewData[\"SuggestedWorkaroundJson\"]");
        detail.Should().NotContain("#b86e00");
        detail.Should().NotContain("btn-outline-");

        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_RunDetailScript.cshtml"));
        script.Should().Contain("suggestedWorkaroundUrl");
        script.Should().Contain("SuggestedWorkaround");
        script.Should().Contain("loadSuggestedWorkaroundFromPage()");
        script.Should().Contain("revalidateSuggestedWorkaroundOnce()");
        script.Should().Contain("renderSuggestedWorkaround");
        script.Should().Contain("model.cause");
        script.Should().Contain("au-workaround-counts");
        script.Should().Contain("Copy conditions");
        script.Should().Contain("Open Organization Resource Maps");
        script.Should().Contain("textContent");
        script.Should().NotContain("btn-outline-");
        script.Should().NotContain("data-hold");

        var css = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/css/site.css"));
        css.Should().Contain(".au-workaround-counts dt");
        css.Should().Contain("color: var(--au-muted);");
        css.Should().NotContain("#b86e00");
        css.Should().NotContain("#fff8ee");

        var filler = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/AutomationRunDetailFiller.cs"));
        filler.Should().Contain("SuggestedWorkaroundBuilder.Match");
        filler.Should().Contain("SuggestedWorkaroundBuilder.ResolveOrgLocation");
        filler.Should().Contain("GetDomainAsync<T>(runId, \"orgLocation\", cancellationToken)");
        filler.Should().Contain("OrganizationMapId(manifest, run.RunConfigurationJson)");
        filler.Should().Contain("ViewData[\"SuggestedWorkaroundJson\"]");

        var controller = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/AutomationController.cs"));
        controller.Should().Contain("[HttpGet(\"Runs/{id:guid}/suggested-workaround\")]");
        controller.Should().Contain("LoadSuggestedWorkaroundAsync");
        controller.Should().Contain("return NoContent();");
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
