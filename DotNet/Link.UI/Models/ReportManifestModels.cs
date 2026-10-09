namespace Link.UI.Models;

/// <summary>Query for the shared report manifest. Lists are paged; page 1 and the default size stay off the address.</summary>
public sealed class ReportManifestQuery
{
    public const int DefaultPageSize = 25;
    public static readonly int[] PageSizes = [10, 25, 50];

    public string? PatientQuery { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public string Sort { get; init; } = "total";
    public bool Descending { get; init; } = true;
    public string? TypeQuery { get; init; }
    public int TypePage { get; init; } = 1;
    public int TypeSize { get; init; } = DefaultPageSize;
    public string? CompareQuery { get; init; }
    public int ComparePage { get; init; } = 1;
    public int CompareSize { get; init; } = DefaultPageSize;
    public int CompareTypePage { get; init; } = 1;
    public string? Stage { get; init; }
    public string? StageMeasure { get; init; }
    public int PopulationPage { get; init; } = 1;
    public string? Tab { get; init; }
}

public sealed class ManifestCountRow
{
    public string Name { get; init; } = string.Empty;
    public int Primary { get; init; }
    public int Secondary { get; init; }
    public int Total { get; init; }
}

public sealed class ManifestPatientRow
{
    public string PatientId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string BadgeClass { get; init; } = "au-badge-muted";
    public string Detail { get; init; } = string.Empty;
    public int Total { get; init; }
    public string? Pattern { get; init; }
    public string? Configuration { get; init; }
    public bool HasBundle { get; init; }
    public string? Href { get; init; }
    public string Submission { get; init; } = string.Empty;
    public string Updated { get; init; } = string.Empty;
    public string ResourceTypes { get; init; } = string.Empty;
    public IReadOnlyList<ManifestMeasureBadge> Measures { get; init; } = [];
    public IReadOnlyList<ManifestResourceCount> Resources { get; init; } = [];
    public IReadOnlyList<ManifestTimelineEvent> Events { get; init; } = [];
    public IReadOnlyList<ManifestLink> Links { get; init; } = [];
    public IReadOnlyList<ManifestResourceRef> ResourceRefs { get; init; } = [];
    public IReadOnlyList<ManifestMembership> Membership { get; init; } = [];
}

/// <summary>One population a patient belongs to. The measure name is the one the report stored.</summary>
public sealed class ManifestMembership
{
    public string Measure { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
}

/// <summary>Plain-English (i) for a domain term. The text is the tooltip.</summary>
public sealed class TermInfo
{
    public string Label { get; init; } = "About this term";
    public string Text { get; init; } = string.Empty;
}

/// <summary>One measure on a patient row. Qualifies is null when the report does not say.</summary>
public sealed class ManifestMeasureBadge
{
    public string Name { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public bool? Qualifies { get; init; }
    public string BadgeClass { get; init; } = "au-badge-muted";
}

public sealed class ManifestResourceCount
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class ManifestTimelineEvent
{
    public string Label { get; init; } = string.Empty;
    public string When { get; init; } = string.Empty;
}

public sealed class ManifestLink
{
    public string Label { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
}

public sealed class ManifestResourceRef
{
    public string Type { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
}

/// <summary>Report-level population figures for one measure. A null count was not in the data.</summary>
public sealed class ManifestPopulationHighlight
{
    public string Measure { get; init; } = string.Empty;
    public int? InitialPopulation { get; init; }
    public int? Denominator { get; init; }
    public int? DenominatorExclusion { get; init; }
    public int? DenominatorException { get; init; }
    public int? Numerator { get; init; }
    public int? NumeratorExclusion { get; init; }
    public string? Rate { get; init; }
    public IReadOnlyList<ManifestCountRow> Other { get; init; } = [];
}

/// <summary>One stage of a measure funnel. A side stage is an exclusion or exception, not the main path.</summary>
public sealed class ManifestFunnelStage
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Help { get; init; }
    public int Count { get; init; }
    public string? Percent { get; init; }
    public int Width { get; init; }
    public bool Side { get; init; }
}

public sealed class ManifestPopulationFunnel
{
    public string Measure { get; init; } = string.Empty;
    public string? Rate { get; init; }
    public IReadOnlyList<ManifestFunnelStage> Stages { get; init; } = [];
}

public sealed class ManifestPopulationCompareRow
{
    public string Measure { get; init; } = string.Empty;
    public int? InitialPopulation { get; init; }
    public int? Denominator { get; init; }
    public int? Numerator { get; init; }
    public string? Rate { get; init; }
}

/// <summary>Automation only. Counts are predictions of the Initial Population, not a scored Numerator.</summary>
public sealed class ManifestPrediction
{
    public string Measure { get; init; } = string.Empty;
    public int Predicted { get; init; }
    public int NotPredicted { get; init; }
    public int? InActual { get; init; }
    public int? MissingFromActual { get; init; }
}

/// <summary>One population on a report, and the measure-report ids that belong to it.</summary>
public sealed class PopulationSlot
{
    public string Measure { get; init; } = string.Empty;
    public string PopulationId { get; init; } = string.Empty;
    public IReadOnlyList<string> MeasureReportIds { get; init; } = [];
}

public sealed class ManifestCompareTypeRow
{
    public string Name { get; init; } = string.Empty;
    public int Generated { get; init; }
    public int Predicted { get; init; }
    public int Actual { get; init; }
    public int Delta { get; init; }
    public string Verdict { get; init; } = string.Empty;
    public string BadgeClass { get; init; } = "au-badge-muted";
    public bool QueryPlan { get; init; }
    public bool Cql { get; init; }
}

public sealed class ManifestComparePatientRow
{
    public string PatientId { get; init; } = string.Empty;
    public int Generated { get; init; }
    public int Predicted { get; init; }
    public int Actual { get; init; }
    public int Delta { get; init; }
    public string Verdict { get; init; } = string.Empty;
    public string BadgeClass { get; init; } = "au-badge-muted";
    public bool HasBundle { get; init; }
    public string Types { get; init; } = string.Empty;
}

public sealed class ManifestComparison
{
    public bool Unavailable { get; init; }
    public int Generated { get; init; }
    public int Predicted { get; init; }
    public int Actual { get; init; }
    public int Delta { get; init; }
    public int Filtered { get; init; }
    public bool HasFilters { get; init; }
    public int MismatchedTypes { get; init; }
    public int MissingPatients { get; init; }
    public IReadOnlyList<ManifestCompareTypeRow> Types { get; init; } = [];
    public PageBar TypePaging { get; init; } = new();
    public IReadOnlyList<ManifestComparePatientRow> Patients { get; init; } = [];
    public PageBar PatientPaging { get; init; } = new();
}

/// <summary>Facts already paged by the report service. The manifest pages the contents list itself.</summary>
public sealed class ReportManifestFacts
{
    public int PatientCount { get; init; }
    public int InitialPopulation { get; init; }
    public int ContentTotal { get; init; }
    public IReadOnlyList<string> Measures { get; init; } = [];
    public IReadOnlyList<ManifestCountRow> Contents { get; init; } = [];
    public string ContentsHeading { get; init; } = "Populations";
    public string? TotalLabel { get; init; }
    public string? PatientResourceLabel { get; init; }
    public IReadOnlyList<ManifestPatientRow> Patients { get; init; } = [];
    public PageBar PatientPaging { get; init; } = new();
    public string? Notice { get; init; }
    public string? PatientNote { get; init; }
    public int? PassedValidation { get; init; }
    public int? FailedValidation { get; init; }
    public int? PendingValidation { get; init; }
    public IReadOnlyList<ManifestCountRow> ResourceTypes { get; init; } = [];
    public IReadOnlyList<ManifestPopulationHighlight> Populations { get; init; } = [];
    public IReadOnlyList<ManifestPatientRow> PopulationPatients { get; init; } = [];
    public PageBar PopulationPaging { get; init; } = new();
    public string? PopulationNote { get; init; }
}

public sealed class ReportManifestModel
{
    public const int ChartCap = 8;

    public string Lead { get; init; } = string.Empty;
    public string? Notice { get; init; }
    public string? PatientNote { get; init; }
    public bool ShowComparison { get; init; }
    public bool ShowGeneration { get; init; }
    public bool ShowShared { get; init; }
    public bool ShowSplit { get; init; }
    public bool ShowTotals { get; init; }
    public Guid? RunId { get; init; }
    public string? BundlePath { get; init; }
    public int? TemplateVersion { get; init; }
    public int? LatestTemplateVersion { get; init; }
    public bool TemplateStale { get; init; }

    public int PatientCount { get; init; }
    public int TotalResourceCount { get; init; }
    public int PatientResourceCount { get; init; }
    public int SharedResourceCount { get; init; }
    public int TypeCount { get; init; }
    public string? HottestName { get; init; }
    public int HottestCount { get; init; }
    public string TotalLabel { get; init; } = "Total resources";
    public string PatientResourceLabel { get; init; } = "Patient resources";
    public string TypeHeading { get; init; } = "Resource types";
    public string EligibilityText { get; init; } = string.Empty;
    public IReadOnlyList<string> Measures { get; init; } = [];
    public int MeasureCount { get; init; }
    public IReadOnlyList<string> AcquiredTypes { get; init; } = [];
    public IReadOnlyList<string> ParameterTypes { get; init; } = [];
    public IReadOnlyList<string> CqlTypes { get; init; } = [];
    public bool ShowQueryPlan { get; init; }

    public IReadOnlyList<ManifestCountRow> Types { get; init; } = [];
    public PageBar TypePaging { get; init; } = new();
    public IReadOnlyList<ManifestCountRow> ChartTypes { get; init; } = [];
    public IReadOnlyList<ManifestCountRow> StatusChart { get; init; } = [];
    public bool ContentsAreStatus { get; init; }
    public IReadOnlyList<ManifestPopulationHighlight> Populations { get; init; } = [];
    public int PopulationMeasureCount { get; init; }
    public IReadOnlyList<ManifestPopulationFunnel> Funnels { get; init; } = [];
    public IReadOnlyList<ManifestPopulationCompareRow> PopulationComparison { get; init; } = [];
    public IReadOnlyList<ManifestPatientRow> PopulationPatients { get; init; } = [];
    public PageBar PopulationPaging { get; init; } = new();
    public string? PopulationNote { get; init; }
    public ManifestPrediction? Prediction { get; init; }
    public bool PopulationsOpen { get; init; }
    public int? PassedValidation { get; init; }
    public int? FailedValidation { get; init; }
    public int? PendingValidation { get; init; }
    public bool ShowValidation => PassedValidation is not null || FailedValidation is not null || PendingValidation is not null;
    public IReadOnlyList<ManifestPatientRow> ChartPatients { get; init; } = [];
    public IReadOnlyList<ManifestPatientRow> Patients { get; init; } = [];
    public PageBar PatientPaging { get; init; } = new();
    public ManifestComparison? Comparison { get; init; }

    public string DefaultSort { get; init; } = "total";
    public ReportManifestQuery Query { get; init; } = new();
    public string Path { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Route { get; init; } = new Dictionary<string, string>();

    public string Href(
        int? page = null,
        int? typePage = null,
        int? comparePage = null,
        int? compareTypePage = null,
        string? patientQuery = null,
        bool clearPatient = false,
        string? typeQuery = null,
        bool clearType = false,
        string? compareQuery = null,
        bool clearCompare = false,
        string? sort = null,
        bool? descending = null,
        int? pageSize = null,
        int? typeSize = null,
        int? compareSize = null,
        string? stage = null,
        bool clearStage = false,
        string? stageMeasure = null,
        int? populationPage = null,
        string? tab = null)
    {
        var current = Query;
        var nextPatient = clearPatient ? null : patientQuery ?? current.PatientQuery;
        var nextType = clearType ? null : typeQuery ?? current.TypeQuery;
        var nextCompare = clearCompare ? null : compareQuery ?? current.CompareQuery;
        var nextSort = sort ?? current.Sort;
        var nextDescending = descending ?? current.Descending;
        var nextPage = page ?? (patientQuery is not null || clearPatient || sort is not null || descending is not null || pageSize is not null ? 1 : current.Page);
        var nextTypePage = typePage ?? (typeQuery is not null || clearType || typeSize is not null ? 1 : current.TypePage);
        var nextComparePage = comparePage ?? (compareQuery is not null || clearCompare || compareSize is not null ? 1 : current.ComparePage);
        var nextCompareType = compareTypePage ?? current.CompareTypePage;
        var nextSize = pageSize ?? current.PageSize;
        var nextTypeSize = typeSize ?? current.TypeSize;
        var nextCompareSize = compareSize ?? current.CompareSize;
        var nextStage = clearStage ? null : stage ?? current.Stage;
        var nextStageMeasure = clearStage ? null : stageMeasure ?? current.StageMeasure;
        var nextPopulationPage = populationPage ?? (stage is not null || clearStage || pageSize is not null ? 1 : current.PopulationPage);
        var nextTab = tab ?? current.Tab;

        var parts = new List<string>();
        foreach (var pair in Route)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
                parts.Add(pair.Key + "=" + Uri.EscapeDataString(pair.Value));
        }

        Add(parts, "q", nextPatient);
        Add(parts, "typeQ", nextType);
        Add(parts, "cmpQ", nextCompare);
        if (nextPage > 1)
            parts.Add("page=" + nextPage);
        if (nextTypePage > 1)
            parts.Add("typePage=" + nextTypePage);
        if (nextComparePage > 1)
            parts.Add("cmpPage=" + nextComparePage);
        if (nextCompareType > 1)
            parts.Add("cmpType=" + nextCompareType);
        if (nextSize != ReportManifestQuery.DefaultPageSize)
            parts.Add("pageSize=" + nextSize);
        if (nextTypeSize != ReportManifestQuery.DefaultPageSize)
            parts.Add("typeSize=" + nextTypeSize);
        if (nextCompareSize != ReportManifestQuery.DefaultPageSize)
            parts.Add("cmpSize=" + nextCompareSize);
        if (!string.Equals(nextSort, DefaultSort, StringComparison.Ordinal) || !nextDescending)
        {
            parts.Add("sort=" + Uri.EscapeDataString(nextSort));
            parts.Add("dir=" + (nextDescending ? "desc" : "asc"));
        }

        Add(parts, "stage", nextStage);
        Add(parts, "stageMeasure", nextStageMeasure);
        if (nextPopulationPage > 1)
            parts.Add("popPage=" + nextPopulationPage);
        if (nextTab is "patients" or "populations" or "comparison")
            parts.Add("tab=" + nextTab);

        return parts.Count == 0 ? Path : Path + "?" + string.Join("&", parts);
    }

    private static void Add(List<string> parts, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(key + "=" + Uri.EscapeDataString(value));
    }
}

public sealed class ReportManifestPage : ReportSectionPage
{
    public ReportManifestModel? Manifest { get; set; }
}
