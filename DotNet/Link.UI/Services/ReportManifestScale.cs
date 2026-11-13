using System.Globalization;
using Link.UI.Models;

namespace Link.UI.Services;

public static partial class ReportManifestRules
{
    /// <summary>
    /// Five thousand patients and one 15,000-resource patient, built as aggregates.
    /// Only the rows on the requested page are created.
    /// </summary>
    public static ReportManifestPage ScaleReport(ReportManifestQuery query, string? returnUrl = null)
    {
        const string measure = "NHSN Acute Care Hospital";
        var indexes = MatchingIndexes(query);
        var bar = Bar(indexes.Count, query.Stage is null ? query.Page : query.PopulationPage, query.PageSize);
        var rows = indexes
            .Skip((bar.Page - 1) * bar.PageSize)
            .Take(bar.PageSize)
            .Select(index => ScaleRow(index, measure))
            .ToList();

        var observation = ResourceGraphRules.ScaleResourceTotal() - ResourceGraphRules.ScaleMixTotal
            + ResourceGraphRules.ScaleMix.First(pair => pair.Type == "Observation").Count;
        var types = ResourceGraphRules.ScaleMix
            .Select(pair => new ManifestCountRow
            {
                Name = pair.Type,
                Primary = pair.Type == "Observation" ? observation : pair.Count,
                Total = pair.Type == "Observation" ? observation : pair.Count
            })
            .OrderByDescending(row => row.Total)
            .ToList();
        var populations = Highlights(
        [
            (measure, "initial-population", 4_200),
            (measure, "denominator", 3_900),
            (measure, "denominator-exclusion", 200),
            (measure, "numerator", 2_100)
        ]);
        var onStage = !string.IsNullOrWhiteSpace(query.Stage);
        var route = new Dictionary<string, string>
        {
            ["facilityId"] = SampleFacilityId,
            ["reportId"] = SampleId.ToString(),
            ["scale"] = "1"
        };
        if (!string.IsNullOrWhiteSpace(returnUrl))
            route["returnUrl"] = returnUrl;

        var manifest = FromReport(
            new ReportManifestFacts
            {
                PatientCount = ResourceGraphRules.ScalePatientCount,
                InitialPopulation = 4_200,
                ContentTotal = ResourceGraphRules.ScaleResourceTotal(),
                Measures = [measure],
                ResourceTypes = types,
                Contents = types,
                ContentsHeading = "Resource types",
                TotalLabel = "Resources",
                PatientResourceLabel = "Initial Population",
                Populations = populations,
                PassedValidation = 4_700,
                FailedValidation = 200,
                PendingValidation = 100,
                Patients = onStage ? [] : rows,
                PatientPaging = onStage
                    ? Paged(1, query.PageSize, ResourceGraphRules.ScalePatientCount)
                    : Paged(bar.Page, bar.PageSize, query.PatientQuery is null ? ResourceGraphRules.ScalePatientCount : indexes.Count),
                PopulationPatients = onStage ? rows : [],
                PopulationPaging = onStage
                    ? Paged(bar.Page, bar.PageSize, indexes.Count)
                    : Paged(1, query.PageSize, 0),
                PopulationNote = onStage && indexes.Count == 0 ? "This report did not store that population." : null
            },
            query,
            "/Reports/Manifest",
            route);

        return new ReportManifestPage
        {
            FacilityId = SampleFacilityId,
            FacilityName = "Fixture hospital",
            ReportId = SampleId.ToString(),
            Report = new FacilityReportRow
            {
                Id = SampleId,
                FacilityId = SampleFacilityId,
                Frequency = "Monthly",
                Measures = measure,
                StatusLabel = "Completed"
            },
            Manifest = manifest
        };
    }

    private static List<int> MatchingIndexes(ReportManifestQuery query)
    {
        var matched = new List<int>();
        var text = query.PatientQuery;
        for (var index = 1; index <= ResourceGraphRules.ScalePatientCount; index++)
        {
            if (!InStage(index, query.Stage))
                continue;
            if (text is not null && !ScaleId(index).Contains(text, StringComparison.OrdinalIgnoreCase))
                continue;
            matched.Add(index);
        }

        if (!query.Descending)
            matched.Reverse();
        return matched;
    }

    private static string ScaleId(int index) => ResourceGraphRules.ScaleId(index);

    private static bool InStage(int index, string? stage) => stage switch
    {
        null or "" => true,
        "initial-population" => index <= 4_200,
        "denominator" => index <= 3_900,
        "denominator-exclusion" => index is >= 3_901 and <= 4_100,
        "numerator" => index <= 2_100,
        _ => false
    };

    private static PageBar Paged(int page, int size, long total)
    {
        var pages = total <= 0 ? 0 : (long)Math.Ceiling(total / (double)size);
        return new PageBar { Page = page, PageSize = size, TotalCount = total, TotalPages = pages };
    }

    private static (int Page, int PageSize) Bar(int total, int page, int size)
    {
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        var current = pages == 0 ? 1 : Math.Min(Math.Max(page, 1), pages);
        return (current, size);
    }

    private static ManifestPatientRow ScaleRow(int index, string measure)
    {
        var id = ScaleId(index);
        var heavy = index == 1;
        var resources = heavy
            ? ResourceGraphRules.ScaleMix
                .Select(pair => new ManifestResourceCount { Name = pair.Type, Count = pair.Count })
                .ToList()
            : new List<ManifestResourceCount> { new() { Name = "Observation", Count = ResourceGraphRules.ScalePatientTotal(index) } };
        var total = resources.Sum(row => row.Count);
        var qualifies = index <= 2_100;
        var inPopulation = index <= 4_200;
        return PatientRow(
            id,
            index % 25 == 0 ? "FailedValidation" : "PassedValidation",
            index % 25 == 0 ? "FailedSubmission" : "Submitted",
            total,
            heavy ? "Observation 9000" : "Observation " + total.ToString(CultureInfo.InvariantCulture),
            "/Reports/Measure?facilityId=" + SampleFacilityId + "&reportId=" + SampleId + "&patientId=" + Uri.EscapeDataString(id),
            updated: "2026-03-11 15:42 UTC",
            measures: inPopulation
                ? [Badge(measure, qualifies ? "In Numerator" : "Not in Numerator", qualifies)]
                : [],
            resources: resources,
            events:
            [
                new ManifestTimelineEvent { Label = "Identified", When = "2026-03-01 08:00 UTC" },
                new ManifestTimelineEvent { Label = "Last updated", When = "2026-03-11 15:42 UTC" }
            ],
            links:
            [
                new ManifestLink
                {
                    Label = "Measure report",
                    Href = "/Reports/Measure?facilityId=" + SampleFacilityId + "&reportId=" + SampleId + "&patientId=" + Uri.EscapeDataString(id)
                }
            ]);
    }
}
