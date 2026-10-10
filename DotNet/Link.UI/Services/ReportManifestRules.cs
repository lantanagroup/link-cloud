using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// Turns a generation snapshot or a report's own patients and populations into one paged manifest.
/// The prediction comparison is optional and stays off for a real report.
/// </summary>
public static partial class ReportManifestRules
{
    public const string SampleFacilityId = "fixture";
    public static readonly Guid SampleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    // Category slices only. Status donuts use green, red, and yellow instead of this list.
    public static readonly string[] DonutColors =
    [
        "#3b82c4", "#2aa89a", "#e0a045", "#d16a8a", "#7b6ad6",
        "#e07a3d", "#4aa3c7", "#6a9a4a", "#c46bb5", "#5c7cfa"
    ];
    public const string DonutOtherColor = "#adb5bd";

    public static string ValidationSliceColor(string? name) => name switch
    {
        "Passed validation" => "var(--au-success)",
        "Failed validation" => "var(--au-danger)",
        "Pending validation" => "var(--au-warning)",
        _ => "#4aa3c7"
    };

    public static string DonutGradient(IReadOnlyList<int> totals, IReadOnlyList<string> colors)
    {
        var sum = 0;
        foreach (var total in totals)
            sum += Math.Max(0, total);
        if (sum <= 0 || colors.Count == 0)
            return "#e6e6e6";

        var parts = new List<string>(totals.Count);
        double cursor = 0;
        for (var index = 0; index < totals.Count; index++)
        {
            var next = cursor + Math.Max(0, totals[index]) * 100.0 / sum;
            var color = colors[Math.Min(index, colors.Count - 1)];
            parts.Add(color + " " + cursor.ToString("0.###", CultureInfo.InvariantCulture) + "% " + next.ToString("0.###", CultureInfo.InvariantCulture) + "%");
            cursor = next;
        }

        return string.Join(", ", parts);
    }

    public static bool IsSample(string? facilityId, string? reportId) =>
        string.Equals(facilityId.Sanitize().Trim(), SampleFacilityId, StringComparison.OrdinalIgnoreCase)
        && Guid.TryParse(reportId.Sanitize().Trim(), out var id)
        && id == SampleId;

    public static ReportManifestQuery Normalize(
        string? patientQuery,
        string? sort,
        string? direction,
        string? typeQuery,
        string? compareQuery,
        int page,
        int pageSize,
        int typePage,
        int typeSize,
        int comparePage,
        int compareSize,
        int compareTypePage,
        string defaultSort,
        string? stage = null,
        string? stageMeasure = null,
        int populationPage = 1,
        string? tab = null)
    {
        var allowed = sort.Sanitize().Trim().ToLowerInvariant();
        if (allowed is not ("total" or "id" or "status"))
            allowed = defaultSort;

        var opened = tab.Sanitize().Trim().ToLowerInvariant();
        if (opened is not ("patients" or "populations" or "comparison"))
            opened = string.Empty;

        return new ReportManifestQuery
        {
            PatientQuery = Blank(patientQuery),
            Sort = allowed,
            Descending = !string.Equals(direction.Sanitize().Trim(), "asc", StringComparison.OrdinalIgnoreCase),
            TypeQuery = Blank(typeQuery),
            CompareQuery = Blank(compareQuery),
            Page = page < 1 ? 1 : page,
            PageSize = ClampSize(pageSize),
            TypePage = typePage < 1 ? 1 : typePage,
            TypeSize = ClampSize(typeSize),
            ComparePage = comparePage < 1 ? 1 : comparePage,
            CompareSize = ClampSize(compareSize),
            CompareTypePage = compareTypePage < 1 ? 1 : compareTypePage,
            Stage = StageKey(stage),
            StageMeasure = Cap(Blank(stageMeasure), 120),
            PopulationPage = populationPage < 1 ? 1 : populationPage,
            Tab = opened.Length == 0 ? null : opened
        };
    }

    public static string Words(string? value)
    {
        var text = value.Sanitize().Trim();
        if (text.Length == 0)
            return string.Empty;

        var spaced = WordBreak().Replace(text, "$1 $2");
        return char.ToUpper(spaced[0], CultureInfo.InvariantCulture) + spaced[1..];
    }

    public static string BadgeClass(string? status)
    {
        var key = (status ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return key switch
        {
            "failedvalidation" or "failedsubmission" or "missingfromabs" or "missing" => "au-badge-danger",
            "passedvalidation" or "submitted" or "match" => "au-badge-success",
            "pendingvalidation" or "submitting" or "partial" or "notreportable" => "au-badge-warning",
            "abshasmore" => "au-badge-active",
            _ => "au-badge-muted"
        };
    }

    public static ManifestPatientRow PatientRow(
        string patientId,
        string? reportingStatus,
        string? submissionStatus,
        int total,
        string? types,
        string? href,
        string? updated = null,
        IReadOnlyList<ManifestMeasureBadge>? measures = null,
        IReadOnlyList<ManifestResourceCount>? resources = null,
        IReadOnlyList<ManifestTimelineEvent>? events = null,
        IReadOnlyList<ManifestLink>? links = null,
        IReadOnlyList<ManifestResourceRef>? resourceRefs = null,
        IReadOnlyList<ManifestMembership>? membership = null)
    {
        var status = Words(reportingStatus);
        if (status.Length == 0)
            status = "Not recorded";

        var submission = Words(submissionStatus);
        var resourceTypes = string.IsNullOrWhiteSpace(types) ? string.Empty : types.Trim();
        var detail = resourceTypes.Length == 0 ? submission : resourceTypes;
        if (detail.Length == 0)
            detail = "No resources recorded";

        return new ManifestPatientRow
        {
            PatientId = patientId.Trim(),
            Status = status,
            BadgeClass = BadgeClass(reportingStatus),
            Detail = detail,
            Submission = submission,
            ResourceTypes = resourceTypes,
            Total = total,
            Updated = updated?.Trim() ?? string.Empty,
            Href = href,
            Measures = measures ?? [],
            Resources = resources ?? [],
            Events = events ?? [],
            Links = links ?? [],
            ResourceRefs = resourceRefs ?? [],
            Membership = membership ?? []
        };
    }

    public static string PopulationLabel(string? populationId) => Classify(populationId) switch
    {
        PopulationKind.InitialPopulation => "Initial Population",
        PopulationKind.Denominator => "Denominator",
        PopulationKind.DenominatorExclusion => "Denominator Exclusion",
        PopulationKind.DenominatorException => "Denominator Exception",
        PopulationKind.Numerator => "Numerator",
        PopulationKind.NumeratorExclusion => "Numerator Exclusion",
        _ => Words(populationId)
    };

    /// <summary>Plain-English help for a measure term. Null when the term is not one of these.</summary>
    public static string? PopulationHelp(string? term) => (term ?? string.Empty).Trim() switch
    {
        "Initial Population" => "Patients who meet the measure's starting rules for this reporting period.",
        "Denominator" => "Patients from the Initial Population who are eligible to be scored.",
        "Denominator Exclusion" => "Patients removed from the Denominator for a reason the measure defines.",
        "Denominator Exception" => "Patients left out of the score because the measure allows an exception.",
        "Numerator" => "Patients in the Denominator who meet the measure's success condition.",
        "Numerator Exclusion" => "Patients removed from the Numerator for a reason the measure defines.",
        "Rate" => "Numerator divided by Denominator. Shown only when both counts are present and the Denominator is greater than zero.",
        _ => null
    };

    public static IReadOnlyList<ManifestPopulationHighlight> Highlights(
        IEnumerable<(string Measure, string PopulationId, int Count)> rows)
    {
        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Measure))
            .GroupBy(row => row.Measure.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                int? initial = null;
                int? denominator = null;
                int? denominatorExclusion = null;
                int? denominatorException = null;
                int? numerator = null;
                int? numeratorExclusion = null;
                var other = new List<ManifestCountRow>();
                foreach (var row in group)
                {
                    var count = Math.Max(0, row.Count);
                    switch (Classify(row.PopulationId))
                    {
                        case PopulationKind.InitialPopulation:
                            initial = (initial ?? 0) + count;
                            break;
                        case PopulationKind.Denominator:
                            denominator = (denominator ?? 0) + count;
                            break;
                        case PopulationKind.DenominatorExclusion:
                            denominatorExclusion = (denominatorExclusion ?? 0) + count;
                            break;
                        case PopulationKind.DenominatorException:
                            denominatorException = (denominatorException ?? 0) + count;
                            break;
                        case PopulationKind.Numerator:
                            numerator = (numerator ?? 0) + count;
                            break;
                        case PopulationKind.NumeratorExclusion:
                            numeratorExclusion = (numeratorExclusion ?? 0) + count;
                            break;
                        default:
                            var name = PopulationLabel(row.PopulationId);
                            if (name.Length > 0)
                                other.Add(new ManifestCountRow { Name = name, Primary = count, Total = count });
                            break;
                    }
                }

                return new ManifestPopulationHighlight
                {
                    Measure = group.Key,
                    InitialPopulation = initial,
                    Denominator = denominator,
                    DenominatorExclusion = denominatorExclusion,
                    DenominatorException = denominatorException,
                    Numerator = numerator,
                    NumeratorExclusion = numeratorExclusion,
                    Rate = numerator is int scored && denominator is > 0
                        ? (scored * 100d / denominator.Value).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                        : null,
                    Other = other
                };
            })
            .OrderByDescending(row => row.InitialPopulation ?? row.Denominator ?? row.Numerator ?? 0)
            .ThenBy(row => row.Measure, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? PopulationKey(string? populationId) => Classify(populationId) switch
    {
        PopulationKind.InitialPopulation => "initial-population",
        PopulationKind.Denominator => "denominator",
        PopulationKind.DenominatorExclusion => "denominator-exclusion",
        PopulationKind.DenominatorException => "denominator-exception",
        PopulationKind.Numerator => "numerator",
        PopulationKind.NumeratorExclusion => "numerator-exclusion",
        _ => null
    };

    public static IReadOnlyList<ManifestMembership> MembershipFor(
        IReadOnlyCollection<string?> patientMeasureReportIds,
        IReadOnlyList<PopulationSlot> slots)
    {
        var owned = new HashSet<string>(
            patientMeasureReportIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (owned.Count == 0 || slots.Count == 0)
            return [];

        var membership = new List<ManifestMembership>();
        foreach (var slot in slots)
        {
            var key = PopulationKey(slot.PopulationId);
            if (key is null || string.IsNullOrWhiteSpace(slot.Measure))
                continue;
            if (!slot.MeasureReportIds.Any(id => owned.Contains(id)))
                continue;
            membership.Add(new ManifestMembership { Measure = slot.Measure.Trim(), Key = key });
        }

        return membership;
    }

    public static int? StageTotal(IReadOnlyList<ManifestPopulationHighlight> highlights, string? measure, string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
            return null;

        var match = highlights.FirstOrDefault(row =>
            string.IsNullOrWhiteSpace(measure)
            || string.Equals(row.Measure, measure, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return null;

        return stage switch
        {
            "initial-population" => match.InitialPopulation,
            "denominator" => match.Denominator,
            "denominator-exclusion" => match.DenominatorExclusion,
            "denominator-exception" => match.DenominatorException,
            "numerator" => match.Numerator,
            "numerator-exclusion" => match.NumeratorExclusion,
            _ => null
        };
    }

    public static (IReadOnlyList<ManifestPatientRow> Page, PageBar Bar, string? Note) PopulationPage(
        IReadOnlyList<ManifestPatientRow> rows,
        string? stage,
        string? measure,
        int page,
        int size,
        int? populationCount)
    {
        if (string.IsNullOrWhiteSpace(stage))
            return ([], new PageBar { Page = 1, PageSize = size }, null);

        var matched = rows.Where(row => row.Membership.Any(item =>
            string.Equals(item.Key, stage, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(measure)
                || string.Equals(item.Measure, measure, StringComparison.OrdinalIgnoreCase)))).ToList();
        var sliced = Slice(matched, page, size);
        string? note = null;
        if (populationCount is int expected && expected != matched.Count)
        {
            var label = PopulationLabel(stage);
            note = "Showing "
                + matched.Count.ToString("N0", CultureInfo.InvariantCulture)
                + " patients from this page who are in the "
                + label
                + ". The funnel count is the whole population. The report service does not search by population, so this is not every patient in it.";
        }

        return (sliced.Page, sliced.Bar, note);
    }

    public static string? PredictionHelp(string? term) => (term ?? string.Empty).Trim() switch
    {
        "Predicted to qualify" => "Patients the run expected to be in the Initial Population. This is a prediction, not a scored Numerator.",
        "Not predicted to qualify" => "Patients the run did not expect to be in the Initial Population.",
        "In the ABS upload" => "Predicted patients who were also in the ABS upload.",
        "Predicted, missing from ABS" => "Predicted patients who were not in the ABS upload.",
        _ => null
    };

    /// <summary>
    /// Badges for a real report. A patient is marked only when their measure report id is in a population
    /// the report actually stored. No numerator is invented for a cohort that only has an Initial Population.
    /// </summary>
    public static IReadOnlyList<ManifestMeasureBadge> ReportBadges(
        IReadOnlyCollection<string?> patientMeasureReportIds,
        IReadOnlyList<PopulationSlot> slots)
    {
        if (slots.Count == 0)
            return [];

        var owned = new HashSet<string>(
            patientMeasureReportIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (owned.Count == 0)
            return [];

        var badges = new List<ManifestMeasureBadge>();
        foreach (var measure in slots.GroupBy(slot => slot.Measure.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (measure.Key.Length == 0)
                continue;

            var known = measure
                .Select(slot => (Kind: Classify(slot.PopulationId), slot))
                .Where(row => row.Kind != PopulationKind.Other)
                .ToList();
            if (known.Count == 0)
                continue;

            var appears = known.Any(row => row.slot.MeasureReportIds.Any(id => owned.Contains(id)));
            if (!appears)
                continue;

            bool In(PopulationKind kind) => known
                .Where(row => row.Kind == kind)
                .Any(row => row.slot.MeasureReportIds.Any(id => owned.Contains(id)));

            if (known.Any(row => row.Kind == PopulationKind.Numerator))
            {
                var qualifies = In(PopulationKind.Numerator);
                badges.Add(Badge(measure.Key, qualifies ? "In Numerator" : "Not in Numerator", qualifies));
            }
            else if (known.Any(row => row.Kind == PopulationKind.InitialPopulation))
            {
                var qualifies = In(PopulationKind.InitialPopulation);
                badges.Add(Badge(measure.Key, qualifies ? "In Initial Population" : "Not in Initial Population", qualifies));
            }
        }

        return badges;
    }

    /// <summary>
    /// Automation stores a predicted Initial Population, not a scored Numerator.
    /// <paramref name="qualifying"/> null means this patient was not in the eligibility map.
    /// </summary>
    public static IReadOnlyList<ManifestMeasureBadge> PredictedBadges(
        IReadOnlyList<string> measures,
        IReadOnlyList<string>? aliases,
        IReadOnlyList<string>? qualifying)
    {
        if (qualifying is null)
            return [];

        var displays = measures.Where(measure => !string.IsNullOrWhiteSpace(measure)).Select(measure => measure.Trim()).ToList();
        var names = aliases?.Where(measure => !string.IsNullOrWhiteSpace(measure)).Select(measure => measure.Trim()).ToList() ?? [];
        var count = Math.Max(displays.Count, names.Count);
        var badges = new List<ManifestMeasureBadge>(count);
        for (var index = 0; index < count; index++)
        {
            var display = index < displays.Count ? displays[index] : names[index];
            var alias = index < names.Count ? names[index] : display;
            var qualifies = qualifying.Any(name =>
                string.Equals(name?.Trim(), display, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name?.Trim(), alias, StringComparison.OrdinalIgnoreCase));
            badges.Add(Badge(display, qualifies ? "Predicted to qualify" : "Not predicted to qualify", qualifies));
        }

        return badges;
    }

    public static IReadOnlyList<ManifestResourceCount> ResourceCounts(IEnumerable<KeyValuePair<string, int>>? counts)
    {
        if (counts is null)
            return [];

        return counts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new ManifestResourceCount { Name = pair.Key.Trim(), Count = pair.Value })
            .ToList();
    }

    public static string? When(DateTime? value)
    {
        if (value is null)
            return null;

        var stamp = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
        return stamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }

    public static ReportManifestModel FromReport(
        ReportManifestFacts facts,
        ReportManifestQuery query,
        string path,
        IReadOnlyDictionary<string, string> route)
    {
        var useResources = facts.ResourceTypes.Count > 0;
        var typeRows = useResources ? facts.ResourceTypes : facts.Contents;
        var heading = useResources ? "Resource types" : facts.ContentsHeading;
        var contentsAreStatus = !useResources
            && string.Equals(heading, "Patients by status", StringComparison.OrdinalIgnoreCase);
        var types = PageTypes(typeRows, query);
        var hottest = types.Sorted.FirstOrDefault();
        var status = new List<ManifestCountRow>();
        if (facts.PassedValidation is int passed)
            status.Add(new ManifestCountRow { Name = "Passed validation", Primary = passed, Total = passed });
        if (facts.FailedValidation is int failed)
            status.Add(new ManifestCountRow { Name = "Failed validation", Primary = failed, Total = failed });
        if (facts.PendingValidation is int pending)
            status.Add(new ManifestCountRow { Name = "Pending validation", Primary = pending, Total = pending });
        return new ReportManifestModel
        {
            Lead = "",
            Notice = null,
            PatientNote = facts.PatientNote,
            ShowComparison = false,
            ShowGeneration = false,
            ShowShared = false,
            ShowSplit = false,
            ShowTotals = facts.Patients.Any(row => row.Total > 0),
            PatientCount = facts.PatientCount,
            TotalResourceCount = facts.ContentTotal,
            PatientResourceCount = facts.InitialPopulation,
            TypeCount = types.Sorted.Count,
            HottestName = hottest?.Name,
            HottestCount = hottest?.Total ?? 0,
            TotalLabel = string.IsNullOrWhiteSpace(facts.TotalLabel) ? "Population total" : facts.TotalLabel,
            PatientResourceLabel = string.IsNullOrWhiteSpace(facts.PatientResourceLabel) ? "Initial population" : facts.PatientResourceLabel,
            TypeHeading = heading,
            ContentsAreStatus = contentsAreStatus,
            EligibilityText = "",
            Measures = facts.Measures,
            MeasureCount = facts.Measures.Count,
            Types = types.Page,
            TypePaging = types.Bar,
            ChartTypes = contentsAreStatus || useResources
                ? types.Sorted.Take(ReportManifestModel.ChartCap).ToList()
                : [],
            StatusChart = status,
            Populations = facts.Populations,
            PopulationMeasureCount = facts.Populations.Count,
            Funnels = facts.Populations.Take(ReportManifestModel.ChartCap).Select(ToFunnel).ToList(),
            PopulationComparison = facts.Populations.Count > 1
                ? facts.Populations.Take(ReportManifestModel.ChartCap).Select(ToCompareRow).ToList()
                : [],
            PopulationPatients = facts.PopulationPatients,
            PopulationPaging = facts.PopulationPaging,
            PopulationNote = facts.PopulationNote,
            PopulationsOpen = string.Equals(query.Tab, "populations", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(query.Stage),
            PassedValidation = facts.PassedValidation,
            FailedValidation = facts.FailedValidation,
            PendingValidation = facts.PendingValidation,
            Patients = facts.Patients,
            PatientPaging = facts.PatientPaging,
            DefaultSort = "status",
            Query = query,
            Path = path,
            Route = route
        };
    }

    public static ReportManifestModel FromGeneration(
        GenerationManifestSnapshot snapshot,
        AbsUploadSnapshot? actual,
        ReportManifestQuery query,
        bool showComparison,
        Guid runId,
        string path,
        string? bundlePath,
        string? runConfigurationJson,
        IReadOnlyDictionary<string, string>? configurationNames,
        int? templateVersion,
        int? latestTemplateVersion,
        string? notice = null)
    {
        var patientCounts = snapshot.TotalCountsByType ?? new Dictionary<string, int>();
        var sharedCounts = snapshot.SharedInfrastructureCountsByType ?? new Dictionary<string, int>();
        var byPatient = snapshot.ResourceCountsByPatient ?? new Dictionary<string, Dictionary<string, int>>();
        var patterns = snapshot.PatientInpatientPatterns ?? new Dictionary<string, string>();
        var eligibility = snapshot.PatientEligibility ?? new Dictionary<string, List<string>>();
        var templates = snapshot.TemplateCacheKeyByPatient ?? new Dictionary<string, string>();
        var ids = PatientIds(snapshot, byPatient);
        var names = configurationNames ?? new Dictionary<string, string>();
        var cohort = CohortMap(ids, runConfigurationJson, names);

        var merged = new Dictionary<string, (int Patient, int Shared)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (type, count) in patientCounts)
            merged[type] = (count, merged.TryGetValue(type, out var have) ? have.Shared : 0);
        foreach (var (type, count) in sharedCounts)
        {
            var have = merged.TryGetValue(type, out var row) ? row : default;
            merged[type] = (have.Patient, count);
        }

        var typeRows = merged
            .Select(pair => new ManifestCountRow
            {
                Name = pair.Key,
                Primary = pair.Value.Patient,
                Secondary = pair.Value.Shared,
                Total = pair.Value.Patient + pair.Value.Shared
            })
            .ToList();
        var types = PageTypes(typeRows, query);

        var needle = query.PatientQuery;
        var patientRows = new List<(string Id, int Total)>(ids.Count);
        foreach (var id in ids)
        {
            if (!string.IsNullOrWhiteSpace(needle)
                && id.Contains(needle, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            patientRows.Add((id, Sum(byPatient.TryGetValue(id, out var counts) ? counts : null)));
        }

        patientRows.Sort((left, right) =>
        {
            var by = query.Sort == "id"
                ? string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase)
                : right.Total.CompareTo(left.Total);
            if (query.Sort == "id" ? query.Descending : !query.Descending)
                by = -by;
            return by != 0 ? by : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        });

        var patientPage = Slice(patientRows, query.Page, query.PageSize);
        var eligibilityKnown = eligibility.Count > 0;
        var measureDisplays = (snapshot.MeasureIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
        var measureAliases = (snapshot.SelectedMeasures ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
        var measureName = measureDisplays.FirstOrDefault() ?? measureAliases.FirstOrDefault() ?? "This run";
        var actualIds = actual?.PatientIds is { } present
            ? new HashSet<string>(present.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal)
            : null;
        bool Predicted(string id) =>
            eligibility.TryGetValue(id, out var list) && list is { Count: > 0 };

        ManifestPatientRow Build(string id, int total)
        {
            byPatient.TryGetValue(id, out var counts);
            eligibility.TryGetValue(id, out var measures);
            patterns.TryGetValue(id, out var pattern);
            cohort.Patterns.TryGetValue(id, out var fallbackPattern);
            cohort.Names.TryGetValue(id, out var configuration);
            var qualifying = measures?.Where(measure => !string.IsNullOrWhiteSpace(measure)).ToList() ?? [];
            var recorded = eligibility.ContainsKey(id);
            var membership = new List<ManifestMembership>();
            if (recorded && qualifying.Count > 0)
            {
                membership.Add(new ManifestMembership { Measure = measureName, Key = "predicted" });
                if (actualIds is not null)
                {
                    membership.Add(new ManifestMembership
                    {
                        Measure = measureName,
                        Key = actualIds.Contains(id) ? "in-actual" : "missing"
                    });
                }
            }

            return new ManifestPatientRow
            {
                PatientId = id,
                Status = qualifying.Count == 0 ? "None" : qualifying.Count + " measure" + (qualifying.Count == 1 ? string.Empty : "s"),
                BadgeClass = qualifying.Count == 0 ? "au-badge-muted" : "au-badge-success",
                Detail = TopTypes(counts, 4),
                ResourceTypes = TopTypes(counts, 4),
                Total = total,
                Resources = ResourceCounts(counts),
                Measures = eligibilityKnown && recorded
                    ? PredictedBadges(measureDisplays, measureAliases, qualifying)
                    : [],
                Membership = membership,
                Pattern = ShortPattern(string.IsNullOrWhiteSpace(pattern) ? fallbackPattern : pattern),
                Configuration = string.IsNullOrWhiteSpace(configuration) ? "No configuration" : configuration,
                HasBundle = templates.ContainsKey(id)
            };
        }

        var shown = patientPage.Page.Select(row => Build(row.Id, row.Total)).ToList();
        var predicted = ids.Count(Predicted);
        ManifestPrediction? prediction = eligibilityKnown
            ? new ManifestPrediction
            {
                Measure = measureName,
                Predicted = predicted,
                NotPredicted = Math.Max(0, ids.Count - predicted),
                InActual = actualIds is null ? null : ids.Count(id => Predicted(id) && actualIds.Contains(id)),
                MissingFromActual = actualIds is null ? null : predicted - ids.Count(id => Predicted(id) && actualIds.Contains(id))
            }
            : null;
        IReadOnlyList<ManifestPatientRow> populationPatients = [];
        var populationPaging = new PageBar { Page = 1, PageSize = query.PageSize };
        string? populationNote = null;
        if (query.Stage is "predicted" or "not-predicted" or "missing" or "in-actual")
        {
            var stageIds = ids.Where(id => query.Stage switch
            {
                "predicted" => Predicted(id),
                "not-predicted" => !Predicted(id),
                "missing" => actualIds is not null && Predicted(id) && !actualIds.Contains(id),
                "in-actual" => actualIds is not null && Predicted(id) && actualIds.Contains(id),
                _ => false
            }).ToList();
            var sliced = Slice(stageIds, query.PopulationPage, query.PageSize);
            populationPatients = sliced.Page.Select(id =>
            {
                var total = Sum(byPatient.TryGetValue(id, out var counts) ? counts : null);
                return Build(id, total);
            }).ToList();
            populationPaging = sliced.Bar;
        }
        else if (!string.IsNullOrWhiteSpace(query.Stage))
        {
            populationNote = "This run did not store a scored " + PopulationLabel(query.Stage) + ".";
        }

        var hottest = types.Sorted.FirstOrDefault();
        var patientResources = patientCounts.Values.Sum();
        var sharedResources = sharedCounts.Values.Sum();
        ManifestComparison? comparison = null;
        if (showComparison)
            comparison = Compare(snapshot, actual, query, templates);

        return new ReportManifestModel
        {
            Lead = "",
            Notice = null,
            ShowComparison = showComparison,
            ShowGeneration = true,
            ShowShared = true,
            ShowSplit = true,
            ShowTotals = true,
            RunId = runId,
            BundlePath = bundlePath,
            TemplateVersion = templateVersion,
            LatestTemplateVersion = latestTemplateVersion,
            TemplateStale = templateVersion is > 0 && latestTemplateVersion is > 0 && latestTemplateVersion > templateVersion,
            PatientCount = snapshot.PatientCount > 0 ? snapshot.PatientCount : ids.Count,
            TotalResourceCount = snapshot.TotalResourceCount > 0 ? snapshot.TotalResourceCount : patientResources + sharedResources,
            PatientResourceCount = patientResources,
            SharedResourceCount = sharedResources,
            TypeCount = types.Sorted.Count,
            HottestName = hottest?.Name,
            HottestCount = hottest?.Total ?? 0,
            TypeHeading = "Resource types",
            EligibilityText = "",
            Measures = (snapshot.MeasureIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
            MeasureCount = snapshot.MeasureIds?.Count ?? 0,
            AcquiredTypes = snapshot.AcquiredResourceTypes ?? [],
            ParameterTypes = snapshot.ParameterQueryResourceTypes ?? [],
            CqlTypes = snapshot.CqlReferencedResourceTypes ?? [],
            ShowQueryPlan = (snapshot.AcquiredResourceTypes?.Count ?? 0)
                + (snapshot.ParameterQueryResourceTypes?.Count ?? 0)
                + (snapshot.CqlReferencedResourceTypes?.Count ?? 0) > 0,
            Types = types.Page,
            TypePaging = types.Bar,
            ChartTypes = types.Sorted.Take(ReportManifestModel.ChartCap).ToList(),
            ChartPatients = shown.OrderByDescending(row => row.Total).Take(ReportManifestModel.ChartCap).ToList(),
            Patients = shown,
            PatientPaging = patientPage.Bar,
            PopulationPatients = populationPatients,
            PopulationPaging = populationPaging,
            PopulationNote = populationNote,
            Prediction = prediction,
            PopulationsOpen = string.Equals(query.Tab, "populations", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(query.Stage),
            Comparison = comparison,
            DefaultSort = "total",
            Query = query,
            Path = path,
            Route = new Dictionary<string, string> { ["id"] = runId.ToString() }
        };
    }

    public static ReportManifestPage SampleReport(ReportManifestQuery query, string? returnUrl = null)
    {
        const string measure = "NHSN Acute Care Hospital";
        static List<string> SampleKeys(int index)
        {
            var keys = new List<string>();
            var inInitial = index == 1 || (index >= 4 && index <= 38);
            if (!inInitial)
                return keys;
            keys.Add("initial-population");
            if (index is >= 5 and <= 8)
                keys.Add("denominator-exclusion");
            if (index == 1 || index == 4 || (index >= 9 && index <= 36))
                keys.Add("denominator");
            if (index == 4 || (index >= 9 && index <= 28) || index == 36)
                keys.Add("numerator");
            return keys;
        }

        var slots = new[] { "initial-population", "denominator-exclusion", "denominator", "numerator" }
            .Select(key => new PopulationSlot
            {
                Measure = measure,
                PopulationId = key,
                MeasureReportIds = Enumerable.Range(1, 40)
                    .Where(index => SampleKeys(index).Contains(key))
                    .Select(index => "mr-" + index.ToString("00", CultureInfo.InvariantCulture))
                    .ToList()
            })
            .ToList();
        var patients = Enumerable.Range(1, 40).Select(index =>
        {
            var failed = index <= 3;
            var id = index == 1
                ? "11111111-1111-1111-1111-111111111112"
                : "patient-" + index.ToString("00", CultureInfo.InvariantCulture);
            var resources = new List<ManifestResourceCount>
            {
                new() { Name = "Observation", Count = 41 - index },
                new() { Name = "Encounter", Count = 1 }
            };
            var refs = Enumerable.Range(0, index == 1 ? 12 : 2).Select(offset => new ManifestResourceRef
            {
                Type = offset % 2 == 0 ? "Observation" : "Encounter",
                Id = "22222222-2222-2222-2222-" + (index * 100 + offset).ToString("000000000000", CultureInfo.InvariantCulture)
            }).ToList();
            return PatientRow(
                id,
                failed ? "FailedValidation" : "PassedValidation",
                failed ? "FailedSubmission" : "Submitted",
                total: resources.Sum(row => row.Count),
                types: "Observation " + (41 - index) + ", Encounter 1",
                href: "/Reports/Measure?facilityId=" + SampleFacilityId + "&reportId=" + SampleId + "&patientId=" + Uri.EscapeDataString(id),
                updated: "2026-03-11 15:42 UTC",
                measures: ReportBadges(["mr-" + index.ToString("00", CultureInfo.InvariantCulture)], slots),
                membership: SampleKeys(index)
                    .Select(key => new ManifestMembership { Measure = measure, Key = key })
                    .ToList(),
                resources: resources,
                events:
                [
                    new ManifestTimelineEvent { Label = "Identified", When = "2026-03-01 08:00 UTC" },
                    new ManifestTimelineEvent { Label = "Acquisition evaluated", When = "2026-03-02 09:15 UTC" },
                    new ManifestTimelineEvent { Label = "Normalization evaluated", When = "2026-03-02 09:40 UTC" },
                    new ManifestTimelineEvent { Label = "Last updated", When = "2026-03-11 15:42 UTC" }
                ],
                links:
                [
                    new ManifestLink
                    {
                        Label = "Measure report",
                        Href = "/Reports/Measure?facilityId=" + SampleFacilityId + "&reportId=" + SampleId + "&patientId=" + Uri.EscapeDataString(id)
                    },
                    new ManifestLink
                    {
                        Label = "Acquisition log",
                        Href = "/Logs/Acquisition?facilityId=" + SampleFacilityId + "&reportId=" + SampleId + "&patientId=" + Uri.EscapeDataString(id)
                    },
                    new ManifestLink
                    {
                        Label = "Audit log",
                        Href = "/Logs/Audit?facilityId=" + SampleFacilityId + "&searchText=" + Uri.EscapeDataString(id)
                    }
                ],
                resourceRefs: refs);
        }).ToList();
        var resourceTypes = new (string Name, int Total)[]
        {
            ("Observation", 840),
            ("Encounter", 40),
            ("Condition", 210),
            ("Procedure", 96),
            ("MedicationRequest", 180),
            ("DiagnosticReport", 64),
            ("AllergyIntolerance", 22),
            ("ServiceRequest", 48),
            ("Specimen", 30),
            ("Patient", 40)
        }.Select(row => new ManifestCountRow { Name = row.Name, Primary = row.Total, Total = row.Total }).ToList();
        var page = Slice(patients, query.Page, query.PageSize);
        var populations = Highlights(
        [
            (measure, "initial-population", 36),
            (measure, "denominator", 30),
            (measure, "denominator-exclusion", 4),
            (measure, "numerator", 22)
        ]);
        var members = PopulationPage(
            patients,
            query.Stage,
            query.StageMeasure,
            query.PopulationPage,
            query.PageSize,
            StageTotal(populations, query.StageMeasure, query.Stage));
        var route = new Dictionary<string, string>
        {
            ["facilityId"] = SampleFacilityId,
            ["reportId"] = SampleId.ToString()
        };
        if (!string.IsNullOrWhiteSpace(returnUrl))
            route["returnUrl"] = returnUrl;
        var manifest = FromReport(
            new ReportManifestFacts
            {
                PatientCount = patients.Count,
                InitialPopulation = 36,
                ContentTotal = resourceTypes.Sum(row => row.Total),
                Measures = [measure],
                ResourceTypes = resourceTypes,
                Contents = resourceTypes,
                ContentsHeading = "Resource types",
                TotalLabel = "Resources",
                PatientResourceLabel = "Initial Population",
                Populations = populations,
                PassedValidation = 37,
                FailedValidation = 3,
                PendingValidation = 0,
                Patients = page.Page,
                PatientPaging = page.Bar,
                PopulationPatients = members.Page,
                PopulationPaging = members.Bar,
                PopulationNote = members.Note
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
                Measures = "NHSN Acute Care Hospital",
                StatusLabel = "Completed"
            },
            Manifest = manifest
        };
    }

    public static ReportManifestModel SampleAutomation(ReportManifestQuery query, string path, string? bundlePath)
    {
        var ids = Enumerable.Range(1, 40).Select(index => "patient-" + index.ToString("00", CultureInfo.InvariantCulture)).ToList();
        var byPatient = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var expected = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var actualPatients = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var eligibility = new Dictionary<string, List<string>>();
        for (var index = 0; index < ids.Count; index++)
        {
            var total = (ids.Count - index) * 4;
            byPatient[ids[index]] = new Dictionary<string, int> { ["Observation"] = total, ["Encounter"] = 1 };
            expected[ids[index]] = new Dictionary<string, int> { ["Observation"] = Math.Max(0, total - 2), ["Encounter"] = 1 };
            if (index >= 3)
                actualPatients[ids[index]] = new Dictionary<string, int> { ["Observation"] = Math.Max(0, total - 2), ["Encounter"] = 1 };
            eligibility[ids[index]] = index % 2 == 0
                ? ["NhsnAcuteCareHospitalMonthlyInitialPopulation"]
                : [];
        }

        var snapshot = new GenerationManifestSnapshot
        {
            PatientCount = ids.Count,
            TotalResourceCount = byPatient.Sum(pair => pair.Value.Values.Sum()) + 2,
            PatientIds = ids,
            MeasureIds = ["NHSN Acute Care Hospital Monthly"],
            SelectedMeasures = ["NhsnAcuteCareHospitalMonthlyInitialPopulation"],
            AcquiredResourceTypes = ["Observation", "Encounter", "Patient"],
            ParameterQueryResourceTypes = ["Encounter"],
            CqlReferencedResourceTypes = ["Observation", "Patient"],
            TotalCountsByType = new Dictionary<string, int>
            {
                ["Observation"] = byPatient.Sum(pair => pair.Value["Observation"]),
                ["Encounter"] = ids.Count
            },
            SharedInfrastructureCountsByType = new Dictionary<string, int> { ["Organization"] = 2 },
            ResourceCountsByPatient = byPatient,
            PatientEligibility = eligibility,
            ExpectedAbsCountsByPatient = expected,
            ExpectedAbsTotalCountsByType = new Dictionary<string, int>
            {
                ["Observation"] = expected.Sum(pair => pair.Value["Observation"]),
                ["Encounter"] = ids.Count
            },
            TemplateCacheKeyByPatient = ids.ToDictionary(id => id, _ => "template", StringComparer.Ordinal)
        };
        var actual = new AbsUploadSnapshot
        {
            TotalResourceCount = actualPatients.Sum(pair => pair.Value.Values.Sum()),
            PatientIds = actualPatients.Keys.ToList(),
            TotalCountsByType = new Dictionary<string, int>
            {
                ["Observation"] = actualPatients.Sum(pair => pair.Value.GetValueOrDefault("Observation")),
                ["Encounter"] = actualPatients.Count
            },
            ResourceCountsByPatient = actualPatients
        };

        return FromGeneration(
            snapshot,
            actual,
            query,
            showComparison: true,
            SampleId,
            path,
            bundlePath,
            runConfigurationJson: null,
            configurationNames: null,
            templateVersion: 3,
            latestTemplateVersion: 4);
    }

    private static ManifestComparison Compare(
        GenerationManifestSnapshot snapshot,
        AbsUploadSnapshot? actual,
        ReportManifestQuery query,
        IReadOnlyDictionary<string, string> templates)
    {
        if (actual is null)
        {
            return new ManifestComparison { Unavailable = true };
        }

        var generated = snapshot.TotalCountsByType ?? new Dictionary<string, int>();
        var predicted = snapshot.ExpectedAbsTotalCountsByType ?? new Dictionary<string, int>();
        var actualCounts = actual.TotalCountsByType ?? new Dictionary<string, int>();
        var acquired = new HashSet<string>(snapshot.AcquiredResourceTypes ?? [], StringComparer.OrdinalIgnoreCase);
        var cql = new HashSet<string>(snapshot.CqlReferencedResourceTypes ?? [], StringComparer.OrdinalIgnoreCase);
        var hasFilters = acquired.Count > 0 || cql.Count > 0;
        var names = generated.Keys.Concat(predicted.Keys).Concat(actualCounts.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var typeRows = names.Select(name =>
        {
            var generatedCount = CountOf(generated, name);
            var predictedCount = CountOf(predicted, name);
            var actualCount = CountOf(actualCounts, name);
            var delta = actualCount - predictedCount;
            var (verdict, badge) = TypeVerdict(generatedCount, predictedCount, actualCount);
            var key = name.ToLowerInvariant();
            return new ManifestCompareTypeRow
            {
                Name = name,
                Generated = generatedCount,
                Predicted = predictedCount,
                Actual = actualCount,
                Delta = delta,
                Verdict = verdict,
                BadgeClass = badge,
                QueryPlan = acquired.Count == 0 || acquired.Contains(name) || key == "patient",
                Cql = cql.Count == 0 || cql.Contains(name) || key == "patient"
            };
        }).ToList();
        var needle = query.CompareQuery;
        if (!string.IsNullOrWhiteSpace(needle))
            typeRows = typeRows.Where(row => row.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();

        typeRows.Sort((left, right) =>
        {
            var rank = TypeRank(left).CompareTo(TypeRank(right));
            if (rank != 0)
                return rank;
            var gap = Math.Abs(right.Delta).CompareTo(Math.Abs(left.Delta));
            return gap != 0 ? gap : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });
        var typePage = Slice(typeRows, query.CompareTypePage, query.CompareSize);

        var generatedPatients = snapshot.ResourceCountsByPatient ?? new Dictionary<string, Dictionary<string, int>>();
        var predictedPatients = snapshot.ExpectedAbsCountsByPatient ?? new Dictionary<string, Dictionary<string, int>>();
        var actualPatients = actual.ResourceCountsByPatient ?? new Dictionary<string, Dictionary<string, int>>();
        var patientIds = generatedPatients.Keys.Concat(predictedPatients.Keys).Concat(actualPatients.Keys)
            .Distinct(StringComparer.Ordinal)
            .Where(id => string.IsNullOrWhiteSpace(needle) || id.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var patientRows = patientIds.Select(id =>
        {
            generatedPatients.TryGetValue(id, out var generatedCounts);
            predictedPatients.TryGetValue(id, out var predictedCounts);
            var known = actualPatients.TryGetValue(id, out var actualPatient);
            var generatedTotal = Sum(generatedCounts);
            var predictedTotal = Sum(predictedCounts);
            var actualTotal = Sum(actualPatient);
            var delta = actualTotal - predictedTotal;
            string verdict;
            string badge;
            if (!known)
            {
                verdict = "Missing from ABS";
                badge = "au-badge-danger";
            }
            else if (delta == 0)
            {
                verdict = "Match";
                badge = "au-badge-success";
            }
            else if (delta < 0)
            {
                verdict = "Partial";
                badge = "au-badge-warning";
            }
            else
            {
                verdict = "ABS has more";
                badge = "au-badge-active";
            }

            return new ManifestComparePatientRow
            {
                PatientId = id,
                Generated = generatedTotal,
                Predicted = predictedTotal,
                Actual = actualTotal,
                Delta = delta,
                Verdict = verdict,
                BadgeClass = badge,
                HasBundle = templates.ContainsKey(id),
                Types = CompareTypes(generatedCounts, predictedCounts, actualPatient)
            };
        }).ToList();
        patientRows.Sort((left, right) =>
        {
            var missing = (left.Verdict == "Missing from ABS" ? 0 : 1).CompareTo(right.Verdict == "Missing from ABS" ? 0 : 1);
            if (missing != 0)
                return missing;
            var delta = left.Delta.CompareTo(right.Delta);
            return delta != 0 ? delta : string.Compare(left.PatientId, right.PatientId, StringComparison.OrdinalIgnoreCase);
        });
        var patients = Slice(patientRows, query.ComparePage, query.CompareSize);
        var generatedTotalAll = generated.Values.Sum();
        var predictedTotalAll = predicted.Values.Sum();
        var actualTotalAll = actual.TotalResourceCount > 0 ? actual.TotalResourceCount : actualCounts.Values.Sum();

        return new ManifestComparison
        {
            Generated = generatedTotalAll,
            Predicted = predictedTotalAll,
            Actual = actualTotalAll,
            Delta = actualTotalAll - predictedTotalAll,
            Filtered = Math.Max(0, generatedTotalAll - predictedTotalAll),
            HasFilters = hasFilters,
            MismatchedTypes = typeRows.Count(row => row.Verdict is not "Match"),
            MissingPatients = patientRows.Count(row => row.Verdict == "Missing from ABS"),
            Types = typePage.Page,
            TypePaging = typePage.Bar,
            Patients = patients.Page,
            PatientPaging = patients.Bar
        };
    }

    private static (List<ManifestCountRow> Sorted, IReadOnlyList<ManifestCountRow> Page, PageBar Bar) PageTypes(
        IReadOnlyList<ManifestCountRow> rows,
        ReportManifestQuery query)
    {
        var filtered = rows.Where(row =>
            string.IsNullOrWhiteSpace(query.TypeQuery)
            || row.Name.Contains(query.TypeQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        filtered.Sort((left, right) =>
        {
            var byCount = right.Total.CompareTo(left.Total);
            return byCount != 0 ? byCount : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });
        var page = Slice(filtered, query.TypePage, query.TypeSize);
        return (filtered, page.Page, page.Bar);
    }

    private static (IReadOnlyList<T> Page, PageBar Bar) Slice<T>(IReadOnlyList<T> rows, int page, int size)
    {
        var total = rows.Count;
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        var current = pages == 0 ? 1 : Math.Min(Math.Max(page, 1), pages);
        var taken = pages == 0 ? [] : rows.Skip((current - 1) * size).Take(size).ToList();
        return (taken, new PageBar { Page = current, PageSize = size, TotalCount = total, TotalPages = pages });
    }

    private static List<string> PatientIds(
        GenerationManifestSnapshot snapshot,
        IReadOnlyDictionary<string, Dictionary<string, int>> byPatient)
    {
        if (snapshot.PatientIds is { Count: > 0 })
            return snapshot.PatientIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();

        return byPatient.Keys.ToList();
    }

    private static int Sum(IReadOnlyDictionary<string, int>? counts) => counts?.Values.Sum() ?? 0;

    private static int CountOf(IReadOnlyDictionary<string, int> counts, string name)
    {
        if (counts.TryGetValue(name, out var count))
            return count;

        foreach (var pair in counts)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return 0;
    }

    private static string TopTypes(IReadOnlyDictionary<string, int>? counts, int take)
    {
        if (counts is null || counts.Count == 0)
            return "No resources";

        var ordered = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var shown = string.Join(", ", ordered.Take(take).Select(pair => pair.Key + " " + pair.Value.ToString("N0", CultureInfo.InvariantCulture)));
        var extra = ordered.Count - take;
        return extra > 0 ? shown + " +" + extra : shown;
    }

    private static string CompareTypes(
        IReadOnlyDictionary<string, int>? generated,
        IReadOnlyDictionary<string, int>? predicted,
        IReadOnlyDictionary<string, int>? actual)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (generated is not null)
            names.UnionWith(generated.Keys);
        if (predicted is not null)
            names.UnionWith(predicted.Keys);
        if (actual is not null)
            names.UnionWith(actual.Keys);

        var ordered = names
            .Select(name => (Name: name, Delta: CountOf(actual ?? new Dictionary<string, int>(), name) - CountOf(predicted ?? new Dictionary<string, int>(), name)))
            .OrderBy(row => row.Delta)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Take(4);
        var text = string.Join(", ", ordered.Select(row => row.Name + " " + row.Delta.ToString("+0;-0;0", CultureInfo.InvariantCulture)));
        var extra = names.Count - 4;
        return extra > 0 ? text + " +" + extra : text;
    }

    private static (string Verdict, string Badge) TypeVerdict(int generated, int predicted, int actual)
    {
        var delta = actual - predicted;
        if (delta == 0 && predicted == actual)
            return ("Match", "au-badge-success");
        if (predicted > 0 && actual == 0)
            return ("Missing from ABS", "au-badge-danger");
        if (actual < predicted)
            return ("Partial", "au-badge-warning");
        if (actual > predicted)
            return ("ABS has more", "au-badge-active");
        if (generated > 0 && predicted == 0)
            return ("Filtered out", "au-badge-muted");
        return ("—", "au-badge-muted");
    }

    private static int TypeRank(ManifestCompareTypeRow row) => row.Verdict switch
    {
        "Missing from ABS" => 0,
        "Partial" => 1,
        "ABS has more" => 2,
        "Filtered out" => 3,
        _ => 4
    };

    private static string ShortPattern(string? value)
    {
        var key = value.Sanitize().Trim();
        if (key.Length == 0)
            return "Before, still in after";

        return key switch
        {
            "AdmittedBeforePeriodRemainsInpatientAfterPeriod" => "Before, still in after",
            "AdmittedBeforePeriodDischargedDuringPeriod" => "Before, out during",
            "AdmittedDuringPeriodRemainsInpatientAfterPeriod" => "During, still in after",
            "AdmittedDuringPeriodDischargedDuringPeriod" => "During, out during",
            "AdmittedAndDischargedBeforePeriod" => "Entirely before",
            "AdmittedAndDischargedAfterPeriod" => "Entirely after",
            _ => Words(key)
        };
    }

    private static (Dictionary<string, string> Patterns, Dictionary<string, string> Names) CohortMap(
        IReadOnlyList<string> patientIds,
        string? json,
        IReadOnlyDictionary<string, string> names)
    {
        var patterns = new Dictionary<string, string>(StringComparer.Ordinal);
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json) || patientIds.Count == 0)
            return (patterns, labels);

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("patientCohorts", out var cohorts)
                && !document.RootElement.TryGetProperty("PatientCohorts", out cohorts))
                return (patterns, labels);
            if (cohorts.ValueKind != JsonValueKind.Array)
                return (patterns, labels);

            var expanded = new List<(string? Pattern, string? Name)>();
            foreach (var cohort in cohorts.EnumerateArray())
            {
                var count = ReadInt(cohort, "patientCount", "PatientCount");
                var pattern = ReadString(cohort, "scheduledInpatientPattern", "ScheduledInpatientPattern");
                var configurationId = ReadString(cohort, "patientConfigurationId", "PatientConfigurationId");
                var label = "No configuration";
                if (!string.IsNullOrWhiteSpace(configurationId))
                {
                    label = names.TryGetValue(configurationId, out var known) && !string.IsNullOrWhiteSpace(known)
                        ? known
                        : configurationId.Length <= 8 ? configurationId : configurationId[..8];
                }

                for (var index = 0; index < count; index++)
                    expanded.Add((pattern, label));
            }

            var limit = Math.Min(patientIds.Count, expanded.Count);
            for (var index = 0; index < limit; index++)
            {
                if (!string.IsNullOrWhiteSpace(expanded[index].Pattern))
                    patterns[patientIds[index]] = expanded[index].Pattern!;
                labels[patientIds[index]] = expanded[index].Name ?? "No configuration";
            }
        }
        catch (JsonException)
        {
            return (patterns, labels);
        }

        return (patterns, labels);
    }

    private static int ReadInt(JsonElement element, string camel, string pascal)
    {
        if (!Try(element, camel, pascal, out var value))
            return 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? Math.Max(0, number) : 0;
    }

    private static string? ReadString(JsonElement element, string camel, string pascal)
    {
        if (!Try(element, camel, pascal, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        return value.GetString();
    }

    private static bool Try(JsonElement element, string camel, string pascal, out JsonElement value) =>
        element.TryGetProperty(camel, out value) || element.TryGetProperty(pascal, out value);

    private static ManifestMeasureBadge Badge(string name, string outcome, bool qualifies) => new()
    {
        Name = name,
        Outcome = outcome,
        Qualifies = qualifies,
        BadgeClass = qualifies ? "au-badge-success" : "au-badge-danger"
    };

    private static PopulationKind Classify(string? populationId)
    {
        var key = (populationId ?? string.Empty).Trim().Replace(' ', '-').ToLowerInvariant();
        return key switch
        {
            "initial-population" => PopulationKind.InitialPopulation,
            "denominator" => PopulationKind.Denominator,
            "denominator-exclusion" => PopulationKind.DenominatorExclusion,
            "denominator-exception" => PopulationKind.DenominatorException,
            "numerator" => PopulationKind.Numerator,
            "numerator-exclusion" => PopulationKind.NumeratorExclusion,
            _ => PopulationKind.Other
        };
    }

    private enum PopulationKind
    {
        Other,
        InitialPopulation,
        Denominator,
        DenominatorExclusion,
        DenominatorException,
        Numerator,
        NumeratorExclusion
    }

    private static int ClampSize(int size) =>
        ReportManifestQuery.PageSizes.Contains(size) ? size : ReportManifestQuery.DefaultPageSize;

    private static string? Cap(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string? StageKey(string? stage)
    {
        var key = stage.Sanitize().Trim().ToLowerInvariant().Replace(' ', '-');
        return key is "initial-population"
            or "denominator"
            or "denominator-exclusion"
            or "denominator-exception"
            or "numerator"
            or "numerator-exclusion"
            or "predicted"
            or "not-predicted"
            or "missing"
            or "in-actual"
            ? key
            : null;
    }

    private static ManifestPopulationFunnel ToFunnel(ManifestPopulationHighlight highlight)
    {
        var baseline = highlight.InitialPopulation;
        ManifestFunnelStage Stage(string key, string name, int? count, bool side)
        {
            var value = count ?? 0;
            return new ManifestFunnelStage
            {
                Key = key,
                Name = name,
                Help = PopulationHelp(name),
                Count = value,
                Percent = Percent(value, baseline),
                Width = Width(value, baseline),
                Side = side
            };
        }

        var stages = new List<ManifestFunnelStage>();
        if (highlight.InitialPopulation is int)
            stages.Add(Stage("initial-population", "Initial Population", highlight.InitialPopulation, false));
        if (highlight.DenominatorExclusion is int)
            stages.Add(Stage("denominator-exclusion", "Denominator Exclusion", highlight.DenominatorExclusion, true));
        if (highlight.DenominatorException is int)
            stages.Add(Stage("denominator-exception", "Denominator Exception", highlight.DenominatorException, true));
        if (highlight.Denominator is int)
            stages.Add(Stage("denominator", "Denominator", highlight.Denominator, false));
        if (highlight.NumeratorExclusion is int)
            stages.Add(Stage("numerator-exclusion", "Numerator Exclusion", highlight.NumeratorExclusion, true));
        if (highlight.Numerator is int)
            stages.Add(Stage("numerator", "Numerator", highlight.Numerator, false));

        return new ManifestPopulationFunnel
        {
            Measure = highlight.Measure,
            Rate = highlight.Rate,
            Stages = stages
        };
    }

    private static ManifestPopulationCompareRow ToCompareRow(ManifestPopulationHighlight highlight) => new()
    {
        Measure = highlight.Measure,
        InitialPopulation = highlight.InitialPopulation,
        Denominator = highlight.Denominator,
        Numerator = highlight.Numerator,
        Rate = highlight.Rate
    };

    private static string? Percent(int count, int? baseline)
    {
        if (baseline is not > 0)
            return null;
        return (count * 100d / baseline.Value).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static int Width(int count, int? baseline)
    {
        if (baseline is not > 0)
            return count > 0 ? 100 : 0;
        var width = (int)Math.Round(count * 100d / baseline.Value, MidpointRounding.AwayFromZero);
        return Math.Clamp(width, 0, 100);
    }

    private static string? Blank(string? value)
    {
        var text = value.Sanitize().Trim();
        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBreak();
}
