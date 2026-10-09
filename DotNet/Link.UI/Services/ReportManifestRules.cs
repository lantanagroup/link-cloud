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
        string defaultSort)
    {
        var allowed = sort.Sanitize().Trim().ToLowerInvariant();
        if (allowed is not ("total" or "id" or "status"))
            allowed = defaultSort;

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
            CompareTypePage = compareTypePage < 1 ? 1 : compareTypePage
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

    public static ManifestPatientRow PatientRow(string patientId, string? reportingStatus, string? submissionStatus, int total, string? types, string? href)
    {
        var status = Words(reportingStatus);
        if (status.Length == 0)
            status = "Not recorded";

        var detail = string.IsNullOrWhiteSpace(types) ? Words(submissionStatus) : types.Trim();
        if (detail.Length == 0)
            detail = "No resources recorded";

        return new ManifestPatientRow
        {
            PatientId = patientId.Trim(),
            Status = status,
            BadgeClass = BadgeClass(reportingStatus),
            Detail = detail,
            Total = total,
            Href = href
        };
    }

    public static ReportManifestModel FromReport(
        ReportManifestFacts facts,
        ReportManifestQuery query,
        string path,
        IReadOnlyDictionary<string, string> route)
    {
        var types = PageTypes(facts.Contents, query);
        var hottest = types.Sorted.FirstOrDefault();
        return new ReportManifestModel
        {
            Lead = "Patients and populations on this report. The list is one page, with failed validation first.",
            Notice = facts.Notice,
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
            TypeHeading = facts.ContentsHeading,
            EligibilityText = facts.PatientCount == 0
                ? "No patients on this report."
                : facts.PatientCount.ToString("N0", CultureInfo.InvariantCulture) + " patients. This page lists " + facts.Patients.Count.ToString("N0", CultureInfo.InvariantCulture) + ".",
            Measures = facts.Measures,
            MeasureCount = facts.Measures.Count,
            Types = types.Page,
            TypePaging = types.Bar,
            ChartTypes = types.Sorted.Take(ReportManifestModel.ChartCap).ToList(),
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
        var shown = patientPage.Page.Select(row =>
        {
            byPatient.TryGetValue(row.Id, out var counts);
            eligibility.TryGetValue(row.Id, out var measures);
            patterns.TryGetValue(row.Id, out var pattern);
            cohort.Patterns.TryGetValue(row.Id, out var fallbackPattern);
            cohort.Names.TryGetValue(row.Id, out var configuration);
            var qualifying = measures?.Where(measure => !string.IsNullOrWhiteSpace(measure)).ToList() ?? [];
            return new ManifestPatientRow
            {
                PatientId = row.Id,
                Status = qualifying.Count == 0 ? "None" : qualifying.Count + " measure" + (qualifying.Count == 1 ? string.Empty : "s"),
                BadgeClass = qualifying.Count == 0 ? "au-badge-muted" : "au-badge-success",
                Detail = TopTypes(counts, 4),
                Total = row.Total,
                Pattern = ShortPattern(string.IsNullOrWhiteSpace(pattern) ? fallbackPattern : pattern),
                Configuration = string.IsNullOrWhiteSpace(configuration) ? "No configuration" : configuration,
                HasBundle = templates.ContainsKey(row.Id)
            };
        }).ToList();

        var qualifyingCount = eligibility.Count(pair => pair.Value is { Count: > 0 });
        var hottest = types.Sorted.FirstOrDefault();
        var patientResources = patientCounts.Values.Sum();
        var sharedResources = sharedCounts.Values.Sum();
        ManifestComparison? comparison = null;
        if (showComparison)
            comparison = Compare(snapshot, actual, query, templates);

        return new ReportManifestModel
        {
            Lead = "Resources this run generated. The largest types and patients are first. Opening this page changes nothing on the run.",
            Notice = notice,
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
            EligibilityText = ids.Count == 0
                ? "No patients on this manifest."
                : qualifyingCount.ToString("N0", CultureInfo.InvariantCulture) + " of " + ids.Count.ToString("N0", CultureInfo.InvariantCulture) + " patients qualify.",
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
            Comparison = comparison,
            DefaultSort = "total",
            Query = query,
            Path = path,
            Route = new Dictionary<string, string> { ["id"] = runId.ToString() }
        };
    }

    public static ReportManifestPage SampleReport(ReportManifestQuery query, string? returnUrl = null)
    {
        var patients = Enumerable.Range(1, 40).Select(index =>
        {
            var failed = index <= 3;
            return PatientRow(
                "patient-" + index.ToString("00", CultureInfo.InvariantCulture),
                failed ? "FailedValidation" : "PassedValidation",
                failed ? "FailedSubmission" : "Submitted",
                total: (41 - index) * 3,
                types: "Observation " + (41 - index),
                href: null);
        }).ToList();
        var contents = Enumerable.Range(1, 12).Select(index => new ManifestCountRow
        {
            Name = index == 1 ? "Initial population" : "Population " + index,
            Total = (13 - index) * 20,
            Primary = (13 - index) * 20
        }).ToList();
        var page = Slice(patients, query.Page, query.PageSize);
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
                ContentTotal = contents.Sum(row => row.Total),
                Measures = ["NHSN Acute Care Hospital"],
                Contents = contents,
                ContentsHeading = "Populations",
                Patients = page.Page,
                PatientPaging = page.Bar,
                Notice = "Fixture data. Nothing here was read from a report service."
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
            if (index % 2 == 0)
                eligibility[ids[index]] = ["NhsnAcuteCareHospitalMonthlyInitialPopulation"];
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
            latestTemplateVersion: 4,
            notice: "Fixture data. Nothing here was read from a run.");
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

    private static int ClampSize(int size) =>
        ReportManifestQuery.PageSizes.Contains(size) ? size : ReportManifestQuery.DefaultPageSize;

    private static string? Blank(string? value)
    {
        var text = value.Sanitize().Trim();
        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBreak();
}
