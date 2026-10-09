using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ReportManifestRulesTests
{
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
