using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ReportManifestRulesTests
{
    [Fact]
    public void A_donut_gradient_splits_the_circle_by_count()
    {
        ReportManifestRules.DonutGradient([1, 1], ["#111111", "#28a745"])
            .Should().Be("#111111 0% 50%, #28a745 50% 100%");
        ReportManifestRules.DonutGradient([], ["#111111"]).Should().Be("#e6e6e6");
        ReportManifestRules.DonutGradient([0], ["#111111"]).Should().Be("#e6e6e6");
    }

    [Fact]
    public void Donut_colors_stay_chromatic_and_status_slices_keep_their_meaning()
    {
        ReportManifestRules.DonutColors.Should().OnlyHaveUniqueItems();
        ReportManifestRules.DonutColors.Length.Should().BeGreaterThanOrEqualTo(8);
        foreach (var color in ReportManifestRules.DonutColors.Append(ReportManifestRules.DonutOtherColor))
            IsDarkGrey(color).Should().BeFalse("slice " + color + " reads as black or dark grey");

        ReportManifestRules.ValidationSliceColor("Passed validation").Should().Be("var(--au-success)");
        ReportManifestRules.ValidationSliceColor("Failed validation").Should().Be("var(--au-danger)");
        ReportManifestRules.ValidationSliceColor("Pending validation").Should().Be("var(--au-warning)");
        IsDarkGrey(ReportManifestRules.ValidationSliceColor("Anything else")).Should().BeFalse();

        var palette = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "js", "chart-palette.js"));
        var category = Regex.Match(palette, @"var category = \[([\s\S]*?)\];");
        category.Success.Should().BeTrue();
        Regex.Matches(category.Groups[1].Value, @"#[0-9a-fA-F]{6}")
            .Select(match => match.Value)
            .Should().Equal(ReportManifestRules.DonutColors);
        foreach (Match hex in Regex.Matches(palette, @"#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b"))
            IsDarkGrey(hex.Value).Should().BeFalse(hex.Value);

        palette.Should().Contain("\"#dc3545\"");
        palette.Should().Contain("\"#ffc107\"");
        palette.Should().Contain("pendingvalidation: \"#ffc107\"");
        palette.Should().Contain("pending: \"#ffc107\"");
        palette.Should().Contain("failedsubmission: \"#dc3545\"");
        palette.Should().Contain("passedvalidation: success");
        palette.Should().Contain("dashboardStatus = [success, \"#dc3545\", \"#ffc107\"");
        palette.Should().NotContain("#343a40");
        palette.Should().NotContain("#111");
        palette.Should().NotContain("#545c64");
        palette.Should().NotContain("#1a1a1a");

        var graph = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "js", "resource-graph.js"));
        graph.Should().Contain("luChartPalette.manifest");
        graph.Should().Contain("hubColor()");
        graph.Should().NotContain("#3b82c4");
        graph.Should().NotContain("#245f96");
        graph.Should().NotContain("59,130,196");
        graph.Should().NotContain("#343a40");
        graph.Should().NotContain("#111111");
        graph.Should().NotContain("\"#111\"");

        var view = File.ReadAllText(Path.Combine(ProjectRoot(), "Views", "Shared", "_ReportManifest.cshtml"));
        view.Should().Contain("ValidationSliceColor");
        view.Should().NotContain("#545c64");
        view.Should().NotContain("#343a40");
        view.Should().NotContain("#111111");
    }

    [Fact]
    public void Page_size_stays_on_the_allowed_set()
    {
        var query = ReportManifestRules.Normalize("  ", "nope", "asc", null, null, 0, 15, 0, 0, 0, 1000, -1, "total");

        query.Page.Should().Be(1);
        query.TypePage.Should().Be(1);
        query.ComparePage.Should().Be(1);
        query.CompareTypePage.Should().Be(1);
        query.PageSize.Should().Be(25);
        query.TypeSize.Should().Be(25);
        query.CompareSize.Should().Be(25);
        query.Sort.Should().Be("total");
        query.Descending.Should().BeFalse();
        query.PatientQuery.Should().BeNull();

        ReportManifestRules.Normalize(null, "id", "desc", null, null, 2, 10, 1, 50, 3, 25, 2, "status")
            .PageSize.Should().Be(10);
    }

    [Fact]
    public void Types_are_largest_first_and_a_search_filters_them()
    {
        var model = ReportManifestRules.FromGeneration(
            Snapshot(typeCount: 12, patientCount: 4),
            actual: null,
            Query(typeSize: 10),
            showComparison: false,
            Guid.NewGuid(),
            "/Automation/manifest",
            bundlePath: null,
            runConfigurationJson: null,
            configurationNames: null,
            templateVersion: null,
            latestTemplateVersion: null);

        model.ShowComparison.Should().BeFalse();
        model.ShowGeneration.Should().BeTrue();
        model.Comparison.Should().BeNull();
        model.Types.Should().HaveCount(10);
        model.Types[0].Name.Should().Be("Type00");
        model.Types[0].Total.Should().BeGreaterThan(model.Types[1].Total);
        model.TypePaging.TotalCount.Should().Be(12);
        model.ChartTypes.Should().HaveCount(ReportManifestModel.ChartCap);
        model.ChartTypes[0].Name.Should().Be("Type00");

        var filtered = ReportManifestRules.FromGeneration(
            Snapshot(typeCount: 12, patientCount: 4),
            actual: null,
            Query(typeQuery: "type11", typeSize: 10),
            showComparison: false,
            Guid.NewGuid(),
            "/Automation/manifest",
            bundlePath: null,
            runConfigurationJson: null,
            configurationNames: null,
            templateVersion: null,
            latestTemplateVersion: null);
        filtered.Types.Should().ContainSingle();
        filtered.Types[0].Name.Should().Be("Type11");
    }

    [Fact]
    public void Patients_are_largest_first_and_a_search_filters_them()
    {
        var model = ReportManifestRules.FromGeneration(
            Snapshot(typeCount: 2, patientCount: 30),
            actual: null,
            Query(pageSize: 10),
            showComparison: false,
            Guid.NewGuid(),
            "/Automation/manifest",
            bundlePath: null,
            runConfigurationJson: null,
            configurationNames: null,
            templateVersion: null,
            latestTemplateVersion: null);

        model.Patients.Should().HaveCount(10);
        model.Patients.Select(row => row.Total).Should().BeInDescendingOrder();
        model.Patients[0].PatientId.Should().Be("patient-01");
        model.PatientPaging.TotalCount.Should().Be(30);
        model.PatientPaging.TotalPages.Should().Be(3);
        model.ChartPatients.Should().HaveCount(ReportManifestModel.ChartCap);

        var filtered = ReportManifestRules.FromGeneration(
            Snapshot(typeCount: 2, patientCount: 30),
            actual: null,
            Query(patientQuery: "patient-2", pageSize: 25),
            showComparison: false,
            Guid.NewGuid(),
            "/Automation/manifest",
            bundlePath: null,
            runConfigurationJson: null,
            configurationNames: null,
            templateVersion: null,
            latestTemplateVersion: null);
        filtered.Patients.Should().NotBeEmpty();
        filtered.Patients.Should().OnlyContain(row => row.PatientId.Contains("patient-2", StringComparison.Ordinal));
        filtered.PatientPaging.TotalCount.Should().BeLessThan(30);
    }

    [Fact]
    public void A_patient_missing_from_abs_sorts_ahead_of_a_match()
    {
        var snapshot = Snapshot(typeCount: 2, patientCount: 4);
        var actualPatients = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        for (var index = 2; index <= 4; index++)
        {
            var id = "patient-" + index.ToString("00");
            actualPatients[id] = new Dictionary<string, int> { ["Type00"] = 10 };
        }

        var model = ReportManifestRules.FromGeneration(
            snapshot,
            new AbsUploadSnapshot
            {
                TotalResourceCount = 30,
                PatientIds = actualPatients.Keys.ToList(),
                TotalCountsByType = new Dictionary<string, int> { ["Type00"] = 30 },
                ResourceCountsByPatient = actualPatients
            },
            Query(compareSize: 10),
            showComparison: true,
            ReportManifestRules.SampleId,
            "/Automation/manifest",
            bundlePath: "/Automation/generated-bundle?id=" + ReportManifestRules.SampleId,
            runConfigurationJson: null,
            configurationNames: null,
            templateVersion: 3,
            latestTemplateVersion: 4);

        model.ShowComparison.Should().BeTrue();
        model.TemplateStale.Should().BeTrue();
        model.Comparison.Should().NotBeNull();
        model.Comparison!.Unavailable.Should().BeFalse();
        model.Comparison.Patients.Should().NotBeEmpty();
        model.Comparison.Patients[0].Verdict.Should().Be("Missing from ABS");
        model.Comparison.Patients[0].BadgeClass.Should().Be("au-badge-danger");
        model.Comparison.MissingPatients.Should().BeGreaterThan(0);
        model.Comparison.Patients.Should().Contain(row => row.Verdict == "Match" || row.Verdict == "Partial" || row.Verdict == "ABS has more");
    }

    [Fact]
    public void A_real_report_pages_status_counts_and_hides_generation()
    {
        var contents = Enumerable.Range(0, 12).Select(index => new ManifestCountRow
        {
            Name = "Status " + index.ToString("00"),
            Primary = 120 - index,
            Total = 120 - index
        }).ToList();
        var facts = new ReportManifestFacts
        {
            PatientCount = 40,
            InitialPopulation = 12,
            ContentTotal = contents.Sum(row => row.Total),
            Measures = ["NHSN Acute Care Hospital"],
            Contents = contents,
            ContentsHeading = "Patients by status",
            TotalLabel = "Patients",
            Patients =
            [
                ReportManifestRules.PatientRow("11111111-1111-1111-1111-111111111112", "FailedValidation", "FailedSubmission", 0, null, "/Reports/Measure?patientId=1")
            ],
            PatientPaging = new PageBar { Page = 1, PageSize = 25, TotalCount = 40, TotalPages = 2 },
            Notice = "Fixture data. Nothing here was read from a report service."
        };

        var model = ReportManifestRules.FromReport(
            facts,
            ReportManifestRules.Normalize(null, null, null, null, null, 1, 25, 1, 25, 1, 25, 1, "status"),
            "/Reports/Manifest",
            new Dictionary<string, string> { ["facilityId"] = "fixture", ["reportId"] = ReportManifestRules.SampleId.ToString() });

        model.ShowComparison.Should().BeFalse();
        model.ShowGeneration.Should().BeFalse();
        model.ShowShared.Should().BeFalse();
        model.DefaultSort.Should().Be("status");
        model.TypeHeading.Should().Be("Patients by status");
        model.TotalLabel.Should().Be("Patients");
        model.Types.Should().HaveCount(12);
        model.Types[0].Total.Should().Be(120);
        model.ChartTypes.Should().HaveCount(ReportManifestModel.ChartCap);
        model.Patients.Should().ContainSingle();
        model.Patients[0].BadgeClass.Should().Be("au-badge-danger");
        model.Patients[0].Status.Should().Be("Failed Validation");
        model.Href().Should().Be("/Reports/Manifest?facilityId=fixture&reportId=" + ReportManifestRules.SampleId);
        model.Patients.Should().OnlyContain(row => row.Measures.Count == 0);
    }

    [Fact]
    public void A_rate_is_numerator_over_denominator_and_a_missing_denominator_has_none()
    {
        var highlights = ReportManifestRules.Highlights(
        [
            ("NHSN", "initial-population", 36),
            ("NHSN", "denominator", 30),
            ("NHSN", "denominator-exclusion", 4),
            ("NHSN", "numerator", 22),
            ("Other", "denominator", 0),
            ("Other", "numerator", 5),
            ("Custom", "custom-population", 9)
        ]);

        highlights.Should().HaveCount(3);
        highlights[0].Measure.Should().Be("NHSN");
        highlights[0].InitialPopulation.Should().Be(36);
        highlights[0].DenominatorExclusion.Should().Be(4);
        highlights[0].Rate.Should().Be("73.3%");
        highlights.Single(row => row.Measure == "Other").Rate.Should().BeNull();
        highlights.Single(row => row.Measure == "Custom").Other.Should().ContainSingle();
        highlights.Single(row => row.Measure == "Custom").Numerator.Should().BeNull();
        ReportManifestRules.PopulationHelp("Numerator").Should().NotBeNullOrWhiteSpace();
        ReportManifestRules.PopulationLabel("initial-population").Should().Be("Initial Population");
    }

    [Fact]
    public void A_patient_is_marked_only_from_a_population_the_report_stored()
    {
        var slots = new List<PopulationSlot>
        {
            new()
            {
                Measure = "NHSN",
                PopulationId = "numerator",
                MeasureReportIds = ["mr-in"]
            },
            new()
            {
                Measure = "NHSN",
                PopulationId = "initial-population",
                MeasureReportIds = ["mr-in", "mr-out"]
            },
            new()
            {
                Measure = "Cohort",
                PopulationId = "initial-population",
                MeasureReportIds = ["mr-cohort"]
            }
        };

        var inNumerator = ReportManifestRules.ReportBadges(["mr-in"], slots);
        inNumerator.Should().ContainSingle();
        inNumerator[0].Qualifies.Should().BeTrue();
        inNumerator[0].Outcome.Should().Be("In Numerator");
        inNumerator[0].BadgeClass.Should().Be("au-badge-success");

        var outside = ReportManifestRules.ReportBadges(["mr-out"], slots);
        outside.Should().ContainSingle();
        outside[0].Qualifies.Should().BeFalse();
        outside[0].Outcome.Should().Be("Not in Numerator");
        outside[0].BadgeClass.Should().Be("au-badge-danger");

        var cohort = ReportManifestRules.ReportBadges(["mr-cohort"], slots);
        cohort.Should().ContainSingle();
        cohort[0].Name.Should().Be("Cohort");
        cohort[0].Outcome.Should().Be("In Initial Population");

        ReportManifestRules.ReportBadges(["mr-unknown"], slots).Should().BeEmpty();
        ReportManifestRules.ReportBadges([], slots).Should().BeEmpty();
    }

    [Fact]
    public void A_run_badge_is_a_prediction_and_stays_off_when_eligibility_was_not_recorded()
    {
        var marked = ReportManifestRules.PredictedBadges(
            ["NHSN Acute Care Hospital Monthly"],
            ["NhsnAcuteCareHospitalMonthlyInitialPopulation"],
            ["NhsnAcuteCareHospitalMonthlyInitialPopulation"]);
        marked.Should().ContainSingle();
        marked[0].Name.Should().Be("NHSN Acute Care Hospital Monthly");
        marked[0].Qualifies.Should().BeTrue();
        marked[0].Outcome.Should().Be("Predicted to qualify");

        var missed = ReportManifestRules.PredictedBadges(
            ["NHSN Acute Care Hospital Monthly"],
            ["NhsnAcuteCareHospitalMonthlyInitialPopulation"],
            []);
        missed[0].Qualifies.Should().BeFalse();
        missed[0].Outcome.Should().Be("Not predicted to qualify");

        ReportManifestRules.PredictedBadges(
            ["NHSN Acute Care Hospital Monthly"],
            ["NhsnAcuteCareHospitalMonthlyInitialPopulation"],
            null).Should().BeEmpty();
    }

    [Fact]
    public void The_sample_report_is_an_overview_and_the_patient_table_opens_details()
    {
        var page = ReportManifestRules.SampleReport(Query());
        var model = page.Manifest!;
        model.Lead.Should().BeEmpty();
        model.Notice.Should().BeNull();
        model.EligibilityText.Should().BeEmpty();
        model.Populations.Should().ContainSingle();
        model.Populations[0].Rate.Should().Be("73.3%");
        model.ShowValidation.Should().BeTrue();
        model.PassedValidation.Should().Be(37);
        model.FailedValidation.Should().Be(3);
        model.ChartTypes.Should().NotBeEmpty();
        model.StatusChart.Should().HaveCount(3);
        model.ContentsAreStatus.Should().BeFalse();
        model.Patients[0].PatientId.Should().Be("11111111-1111-1111-1111-111111111112");
        model.Patients[0].Measures.Should().ContainSingle();
        model.Patients[0].Measures[0].Qualifies.Should().BeFalse();
        model.Patients[0].ResourceRefs.Should().HaveCount(12);
        model.Patients[0].Links.Should().Contain(link => link.Label == "Acquisition log");
        model.Patients[3].Measures[0].Qualifies.Should().BeTrue();

        var view = File.ReadAllText(Path.Combine(ProjectRoot(), "Views", "Shared", "_ReportManifest.cshtml"));
        view.Should().NotContain("<th>Detail</th>");
        view.Should().Contain(">View</button>");
        view.Should().Contain("id=\"manifestPatientModal\"");
        view.Should().Contain("name = item.Name");
        view.Should().Contain("href = item.Href");
        view.Should().Contain("id = item.Id");
        view.Should().Contain("lu-donut-legend");
        view.Should().Contain("lu-donut-ring");
        view.Should().Contain("ValidationSliceColor");
        view.Should().Contain("id=\"manifest-validation\"");
        view.Should().Contain("lu-size-chip");
        view.Should().Contain("lu-measure-tiles");
        view.Should().Contain("Predicted to qualify");
        view.Should().NotContain("lu-measure-list");
        view.Should().NotContain("lu-nav-current");
        view.Should().Contain("mountDonut");
        view.Should().Contain("Interactive mode");
        view.Should().Contain("resource-graph.js");
        view.Should().Contain("id=\"manifest-resource-explorer\"");
        view.Should().NotContain("Linked resources");
        view.Should().NotContain("Fixture data");
        view.Should().NotContain("Report overview");
        view.Should().NotContain("Largest is");
        view.Should().NotContain("Patients by validation");
        view.Should().NotContain("maintainAspectRatio: true");
        view.IndexOf("lu-section-nav", StringComparison.Ordinal).Should().BeLessThan(view.IndexOf("id=\"manifest-overview\"", StringComparison.Ordinal));
        view.Should().Contain("id=\"manifest-populations\"");

        var automation = ReportManifestRules.SampleAutomation(Query(), "/Automation/manifest", null);
        automation.Populations.Should().BeEmpty();
        automation.Prediction.Should().NotBeNull();
        automation.Prediction!.Measure.Should().Be("NHSN Acute Care Hospital Monthly");
        automation.Prediction.Predicted.Should().Be(20);
        automation.Prediction.NotPredicted.Should().Be(20);

        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "site.css"));
        var marker = css.IndexOf("/* lu-row-action-fill-end */", StringComparison.Ordinal);
        marker.Should().BeGreaterThan(-1);
        var quiet = css[(marker + "/* lu-row-action-fill-end */".Length)..];
        quiet.Should().Contain(".btn.lu-copy");
        quiet.Should().Contain("background-color: transparent");
        var graph = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "js", "resource-graph.js"));
        graph.Should().Contain("lu-icon-quiet");
        graph.Should().Contain("pageSize: 25");
    }

    [Fact]
    public void A_population_funnel_uses_the_initial_population_and_opens_its_patients()
    {
        var model = ReportManifestRules.SampleReport(Stage("numerator", "NHSN Acute Care Hospital")).Manifest!;

        model.PopulationsOpen.Should().BeTrue();
        model.Funnels.Should().ContainSingle();
        model.Funnels[0].Rate.Should().Be("73.3%");
        model.Funnels[0].Stages.Single(stage => stage.Key == "initial-population").Percent.Should().Be("100.0%");
        model.Funnels[0].Stages.Single(stage => stage.Key == "denominator").Percent.Should().Be("83.3%");
        model.Funnels[0].Stages.Single(stage => stage.Key == "denominator-exclusion").Percent.Should().Be("11.1%");
        model.Funnels[0].Stages.Single(stage => stage.Key == "numerator").Percent.Should().Be("61.1%");
        model.PopulationPaging.TotalCount.Should().Be(22);
        model.PopulationNote.Should().BeNull();
        model.PopulationPatients.Should().Contain(row => row.PatientId == "patient-04");
        model.PopulationPatients.Should().NotContain(row => row.PatientId == "11111111-1111-1111-1111-111111111112");
        model.Href(populationPage: 2, tab: "populations").Should().Contain("stage=numerator").And.Contain("popPage=2");

        var initial = ReportManifestRules.SampleReport(Stage("initial-population", "NHSN Acute Care Hospital", page: 2)).Manifest!;
        initial.PopulationPaging.TotalCount.Should().Be(36);
        initial.PopulationPatients.Should().HaveCount(11);

        ReportManifestRules.Normalize(null, null, null, null, null, 1, 25, 1, 25, 1, 25, 1, "status", "<b>", null, 0, "nope")
            .Stage.Should().BeNull();
    }

    [Fact]
    public void Measures_are_compared_largest_initial_population_first()
    {
        var highlights = ReportManifestRules.Highlights(
        [
            ("Narrow", "initial-population", 3),
            ("Narrow", "denominator", 2),
            ("Narrow", "numerator", 1),
            ("Wide", "initial-population", 10),
            ("Wide", "denominator", 8),
            ("Wide", "numerator", 4)
        ]);
        var model = ReportManifestRules.FromReport(
            new ReportManifestFacts { Populations = highlights },
            Query(),
            "/Reports/Manifest",
            new Dictionary<string, string>());

        model.PopulationComparison.Select(row => row.Measure).Should().Equal("Wide", "Narrow");
        model.PopulationComparison[0].Rate.Should().Be("50.0%");
        model.Funnels.Should().HaveCount(2);
    }

    [Fact]
    public void A_run_shows_predicted_patients_against_the_upload()
    {
        var model = ReportManifestRules.SampleAutomation(Query(), "/Automation/manifest", bundlePath: null);

        model.Prediction.Should().NotBeNull();
        model.Prediction!.Predicted.Should().Be(20);
        model.Prediction.NotPredicted.Should().Be(20);
        model.Prediction.InActual.Should().Be(18);
        model.Prediction.MissingFromActual.Should().Be(2);
        model.Funnels.Should().BeEmpty();

        var missing = ReportManifestRules.SampleAutomation(Stage("missing", page: 1, sort: "total"), "/Automation/manifest", bundlePath: null);
        missing.PopulationPaging.TotalCount.Should().Be(2);
        missing.PopulationPatients.Select(row => row.PatientId).Should().BeEquivalentTo(["patient-01", "patient-03"]);
    }

    [Fact]
    public void A_page_that_is_not_the_whole_population_says_so()
    {
        var rows = new[]
        {
            new ManifestPatientRow
            {
                PatientId = "patient-04",
                Membership = [new ManifestMembership { Measure = "NHSN", Key = "numerator" }]
            }
        };

        var page = ReportManifestRules.PopulationPage(rows, "numerator", "NHSN", 1, 25, 22);

        page.Page.Should().ContainSingle();
        page.Note.Should().Contain("not every patient");
    }

    private static ReportManifestQuery Stage(string stage, string? measure = null, int page = 1, string sort = "status") =>
        ReportManifestRules.Normalize(null, null, null, null, null, 1, 25, 1, 25, 1, 25, 1, sort, stage, measure, page, "populations");

    private static bool IsDarkGrey(string color)
    {
        var hex = color.Trim();
        if (hex.StartsWith("var(", StringComparison.Ordinal))
            return false;
        if (!hex.StartsWith('#'))
            return true;
        hex = hex[1..];
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(character => $"{character}{character}"));
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
            return true;

        var red = (packed >> 16) & 255;
        var green = (packed >> 8) & 255;
        var blue = packed & 255;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var average = (red + green + blue) / 3.0;
        if (average < 55)
            return true;
        return max - min < 18 && average < 140;
    }

    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }

    private static ReportManifestQuery Query(
        string? patientQuery = null,
        string? typeQuery = null,
        int pageSize = 25,
        int typeSize = 25,
        int compareSize = 25) =>
        new()
        {
            PatientQuery = patientQuery,
            TypeQuery = typeQuery,
            Sort = "total",
            Descending = true,
            Page = 1,
            PageSize = pageSize,
            TypePage = 1,
            TypeSize = typeSize,
            ComparePage = 1,
            CompareSize = compareSize,
            CompareTypePage = 1
        };

    private static GenerationManifestSnapshot Snapshot(int typeCount, int patientCount)
    {
        var totals = new Dictionary<string, int>();
        for (var index = 0; index < typeCount; index++)
            totals["Type" + index.ToString("00")] = (typeCount - index) * 10;

        var ids = Enumerable.Range(1, patientCount).Select(index => "patient-" + index.ToString("00")).ToList();
        var byPatient = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var expected = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        for (var index = 0; index < ids.Count; index++)
        {
            var total = (ids.Count - index) * 5;
            byPatient[ids[index]] = new Dictionary<string, int> { ["Type00"] = total };
            expected[ids[index]] = new Dictionary<string, int> { ["Type00"] = total };
        }

        return new GenerationManifestSnapshot
        {
            PatientCount = ids.Count,
            TotalResourceCount = byPatient.Sum(pair => pair.Value.Values.Sum()),
            PatientIds = ids,
            MeasureIds = ["NHSN Acute Care Hospital"],
            AcquiredResourceTypes = ["Type00"],
            TotalCountsByType = totals,
            ResourceCountsByPatient = byPatient,
            ExpectedAbsCountsByPatient = expected,
            ExpectedAbsTotalCountsByType = new Dictionary<string, int> { ["Type00"] = expected.Sum(pair => pair.Value["Type00"]) },
            TemplateCacheKeyByPatient = ids.ToDictionary(id => id, _ => "template", StringComparer.Ordinal)
        };
    }
}
