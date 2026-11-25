using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// Run details shows which normalization suite and organization map a run used, whether its
/// patients were generated or uploaded, and the normalization execution evidence. The manifest
/// is the Patient manifest and labels each patient's source.
/// </summary>
public class NormalizationResultsTests
{
    [Fact]
    public void Endpoint_reads_the_run_manifest_evidence_suite_and_map()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/AutomationController.Normalization.cs"));

        text.Should().Contain("[HttpGet(\"normalization-results\")]");
        text.Should().Contain("public async Task<IActionResult> NormalizationResults(Guid id, CancellationToken cancellationToken)");
        text.Should().Contain("if (EngineOff() is { } off)");
        text.Should().Contain("GetGenerationManifestAsync(id, cancellationToken)");
        text.Should().Contain("GetDomainAsync<NormalizationEvidenceSnapshot>(");
        text.Should().Contain("manifest?.AppliedNormalizationSuiteId ?? config.SuiteId");
        text.Should().Contain("manifest?.AppliedOrganizationResourceMapTemplateId ?? config.OrganizationMapId");
        text.Should().Contain("if (suiteId.HasValue && !hasRecordedSequences");
        text.Should().Contain("NormalizationRunResultsBuilder.SuiteDefinitionChangedAfter(");
        text.Should().Contain("NormalizationRunResultsBuilder.Build(new NormalizationRunResultsSource");
        // Exception messages can carry connection details; only the type is logged.
        text.Should().Contain("ex.GetType().Name");
        text.Should().NotContain("LogWarning(ex");
    }

    [Fact]
    public void Run_details_has_the_summary_strip_and_its_drill_down_popups()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_RunDetail.cshtml"));

        view.Should().Contain("id=\"normalizationSummaryStrip\"");
        foreach (var id in new[] { "normalizationSuiteValue", "normalizationOrmValue", "normalizationPatientValue", "normalizationEvidenceValue" })
            view.Should().Contain($"id=\"{id}\"");
        view.Should().Contain("id=\"normalizationResultsBtn\" data-bs-toggle=\"modal\" data-bs-target=\"#normalizationResultsModal\"");
        foreach (var modal in new[] { "normalizationResultsModal", "normalizationSuiteModal", "normalizationOrmModal" })
            view.Should().Contain($"<div class=\"modal fade\" id=\"{modal}\"");
        view.Should().Contain("<i class=\"bi bi-card-checklist me-1\"></i>Patient Manifest");
        view.Should().NotContain("Generation Manifest");
    }

    [Fact]
    public void Run_details_script_loads_results_lazily_and_only_polls_while_pending()
    {
        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_RunDetailScript.cshtml"));

        script.Should().Contain("const normalizationResultsUrl = '@Url.Action(\"NormalizationResults\", \"Automation\")';");
        script.Should().Contain("if (lastNormalizationResults.evidenceState !== 'pending') return;");
        script.Should().Contain("if (Date.now() - normalizationResultsLoadedAt < 15000) return;");
        script.Should().Contain("loadNormalizationResults(false);");
        script.Should().Contain("show.bs.modal', () => renderSuiteSummary(lastNormalizationResults)");
        script.Should().Contain("show.bs.modal', () => renderOrganizationMapSummary(lastNormalizationResults)");

        // The first call runs before the helpers further down, so its state must be declared above it.
        var declared = script.IndexOf("let normalizationResultsLoadedAt = 0;", StringComparison.Ordinal);
        var firstCall = script.IndexOf("loadNormalizationResults(", StringComparison.Ordinal);
        declared.Should().BeGreaterThan(0);
        declared.Should().BeLessThan(firstCall);

        // Link.UI badge palette, not Bootstrap's bg-* colors.
        var start = script.IndexOf("// --- Normalization execution details ---", StringComparison.Ordinal);
        var end = script.IndexOf("// --- Patient Manifest ---", StringComparison.Ordinal);
        var block = script[start..end];
        block.Should().NotContain("bg-primary");
        block.Should().NotContain("bg-secondary");
        block.Should().Contain("escapeHtml(name || fallback)");
        block.Should().Contain("window.luStatusPills.forNormalization(state)");
        File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/status-pills.js"))
            .Should().Contain("forNormalization: forNormalization,");
    }

    [Fact]
    public void Details_popup_does_not_repeat_the_found_evidence_message()
    {
        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_RunDetailScript.cshtml"));

        // The Found label shows the evidence message, so the empty-inventory fallback stays blank for it.
        script.Should().Contain("? '<ul class=\"mb-0\">' + inventory.map(");
        script.Should().Contain(": (data.evidenceState === 'found'");
    }

    [Fact]
    public void Manifest_rows_carry_the_recorded_patient_source()
    {
        var snapshot = new GenerationManifestSnapshot
        {
            PatientCount = 3,
            PatientIds = ["p-1", "p-2", "p-3"],
            ResourceCountsByPatient = new Dictionary<string, Dictionary<string, int>>
            {
                ["p-1"] = new() { ["Encounter"] = 3 },
                ["p-2"] = new() { ["Encounter"] = 2 },
                ["p-3"] = new() { ["Encounter"] = 1 }
            },
            PatientOriginByPatient = new Dictionary<string, string>
            {
                ["p-1"] = PatientOrigin.Generated,
                ["p-2"] = PatientOrigin.Uploaded
            }
        };

        var model = Build(snapshot);

        model.OriginRecorded.Should().BeTrue();
        model.Patients.Single(row => row.PatientId == "p-1").Origin.Should().Be("Generated");
        model.Patients.Single(row => row.PatientId == "p-2").Origin.Should().Be("Uploaded");
        model.Patients.Single(row => row.PatientId == "p-3").Origin.Should().BeNull();
    }

    [Fact]
    public void Older_runs_without_a_source_say_it_was_not_recorded()
    {
        var model = Build(new GenerationManifestSnapshot
        {
            PatientCount = 1,
            PatientIds = ["p-1"],
            ResourceCountsByPatient = new Dictionary<string, Dictionary<string, int>> { ["p-1"] = new() { ["Encounter"] = 1 } }
        });

        model.OriginRecorded.Should().BeFalse();
        model.Patients.Should().ContainSingle().Which.Origin.Should().BeNull();

        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_ReportManifest.cshtml"));
        view.Should().Contain("Patient source was not recorded for this run.");
        view.Should().Contain("<td data-patient-origin>");
    }

    [Theory]
    [InlineData("Generated", "Generated", "au-badge-automation")]
    [InlineData("uploaded", "Uploaded", "au-badge-active")]
    [InlineData("Something", null, null)]
    [InlineData(null, null, null)]
    public void Source_labels_only_the_values_the_engine_writes(string? stored, string? label, string? badge)
    {
        ReportManifestRules.PatientOriginLabel(stored).Should().Be(label);
        if (label is not null)
            ReportManifestRules.PatientOriginBadge(label).Should().Be(badge);
    }

    [Fact]
    public void Manifest_page_is_named_patient_manifest()
    {
        File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/Manifest.cshtml"))
            .Should().Contain("<h1 class=\"h3 mb-3\">Patient manifest</h1>");
        File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/AutomationController.RunDepth.cs"))
            .Should().NotContain("Generation manifest");
    }

    private static ReportManifestModel Build(GenerationManifestSnapshot snapshot) =>
        ReportManifestRules.FromGeneration(
            snapshot,
            actual: null,
            new ReportManifestQuery
            {
                Sort = "total",
                Descending = true,
                Page = 1,
                PageSize = 25,
                TypePage = 1,
                TypeSize = 25,
                ComparePage = 1,
                CompareSize = 25,
                CompareTypePage = 1
            },
            showComparison: false,
            Guid.NewGuid(),
            "/Automation/manifest",
            bundlePath: null,
            runConfigurationJson: null,
            configurationNames: null,
            templateVersion: null,
            latestTemplateVersion: null);

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
