using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// Reports list, ad-hoc generate, download, prequalification, validation, measure-report JSON,
/// and the report's acquisition log. Reads go through LinkSDK.
/// </summary>
public sealed record ReportCountRow(string Id, int? Census, int? Population);

public sealed class ReportActivityLoad
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public ReportActivityCounts? Counts { get; init; }

    public static ReportActivityLoad Failed(string error) => new() { Error = error };

    public static ReportActivityLoad Ready(ReportActivityCounts counts) => new() { Ok = true, Counts = counts };
}

public sealed class ReportsService
{
    public const string ReportNotConfigured =
        "Report service URL is not configured (ServiceRegistry:ReportServiceUrl).";
    public const string SubmissionNotConfigured =
        "Submission service URL is not configured (ServiceRegistry:SubmissionServiceUrl).";
    public const string ValidationNotConfigured =
        "Validation service URL is not configured (ServiceRegistry:ValidationServiceUrl).";
    public const string MeasureNotConfigured =
        "MeasureEval service URL is not configured (ServiceRegistry:MeasureServiceUrl).";

    private readonly IFacilityServiceClient _facilities;
    private readonly IReportServiceClient? _reports;
    private readonly ISubmissionServiceClient? _submission;
    private readonly IValidationServiceClient? _validation;
    private readonly IMeasureEvalServiceClient? _measure;
    private readonly LinkUiFeatureOptions _options;
    private readonly IMemoryCache _counts;
    private readonly ILogger<ReportsService> _logger;
    private readonly Func<CancellationToken, Task<(AutomationOwnershipIndex Index, bool Reachable)>>? _ownership;

    public ReportsService(
        IFacilityServiceClient facilities,
        IReportServiceClient? reports,
        ISubmissionServiceClient? submission,
        IValidationServiceClient? validation,
        IMeasureEvalServiceClient? measure,
        IOptions<LinkUiFeatureOptions> options,
        IMemoryCache counts,
        ILogger<ReportsService> logger,
        Func<CancellationToken, Task<(AutomationOwnershipIndex Index, bool Reachable)>>? ownership = null)
    {
        _facilities = facilities;
        _reports = reports;
        _submission = submission;
        _validation = validation;
        _measure = measure;
        _options = options.Value;
        _counts = counts;
        _logger = logger;
        _ownership = ownership;
    }

    public static ReportsService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        var ownership = services.GetRequiredService<AutomationOwnershipLookup>();
        return new ReportsService(
            services.GetRequiredService<IFacilityServiceClient>(),
            Blank(registry.ReportServiceUrl) ? null : services.GetRequiredService<IReportServiceClient>(),
            Blank(registry.SubmissionServiceUrl) ? null : services.GetRequiredService<ISubmissionServiceClient>(),
            Blank(registry.ValidationServiceUrl) ? null : services.GetRequiredService<IValidationServiceClient>(),
            Blank(registry.MeasureServiceUrl) ? null : services.GetRequiredService<IMeasureEvalServiceClient>(),
            services.GetRequiredService<IOptions<LinkUiFeatureOptions>>(),
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<ILogger<ReportsService>>(),
            ownership.GetSnapshotAsync);
    }

    public async Task<ReportActivityLoad> LoadActivityCountsAsync(int days, CancellationToken cancellationToken)
    {
        if (_reports is null)
            return ReportActivityLoad.Failed(ReportNotConfigured);

        try
        {
            var response = await _reports.GetActivityCountsAsync(
                new ReportActivityCountRequest { Days = days },
                cancellationToken);
            if (!response.IsSuccessStatusCode || response.Body is null)
                return ReportActivityLoad.Failed(FacilityFormRules.ServiceMessage("Report", response.StatusCode, response.RawBody));
            return ReportActivityLoad.Ready(response.Body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report activity counts failed");
            return ReportActivityLoad.Failed("Report service call failed.");
        }
    }

    public async Task<ReportsListModel> LoadListAsync(ReportsListQuery? query, CancellationToken cancellationToken)
    {
        query ??= new ReportsListQuery();
        var page = new ReportsListModel { Query = query };
        var sortBy = FacilityViewRules.AllowedSort(query.SortBy, FacilityViewRules.ReportSorts) ?? "CreateDate";
        var sortDir = FacilityViewRules.ParseSortDirection(query.SortDir) ?? SortOrder.Descending;
        page.SortBy = sortBy;
        page.SortDir = sortDir == SortOrder.Ascending ? "asc" : "desc";

        if (_reports is null)
        {
            page.LoadError = ReportNotConfigured;
            return page;
        }

        var facilityId = FacilityViewRules.Clean(query.FacilityId);
        if (facilityId is not null && !FacilityFormRules.IsValidFacilityId(facilityId, _options.NumericOnlyFacilityId))
        {
            page.LoadError = FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId);
            return page;
        }

        var reportId = FacilityViewRules.ParseReportId(query.ReportId, out var idError);
        if (idError is not null)
        {
            page.LoadError = idError;
            return page;
        }

        try
        {
            var pageSize = FacilityViewRules.ClampPageSize(query.PageSize);
            var pageNumber = FacilityViewRules.ClampPage(query.Page);
            var response = await _reports.SearchFacilitySchedulesAsync(new ReportScheduleSearch
            {
                FacilityId = facilityId,
                Frequency = FacilityViewRules.ParseFrequency(query.Frequency),
                ReportStartDate = FacilityViewRules.PeriodStart(FacilityViewRules.ParseDate(query.PeriodFrom)),
                ReportEndDate = FacilityViewRules.PeriodEnd(FacilityViewRules.ParseDate(query.PeriodTo)),
                Statuses = FacilityViewRules.ParseStatuses(query.Status),
                IncludeDeleted = query.ShowDeleted,
                SortBy = sortBy,
                SortOrder = sortDir,
                PageSize = pageSize,
                PageNumber = pageNumber,
                CreateDate = FacilityViewRules.ParseDate(query.Created),
                Id = reportId
            }, cancellationToken);

            if (response.StatusCode == StatusCodes.Status204NoContent || response.Body is null && response.IsSuccessStatusCode)
            {
                page.Paging = new PageBar { Page = 1, PageSize = pageSize };
                return page;
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Report", response.StatusCode, response.RawBody);
                return page;
            }

            var records = response.Body.Records;
            page.Paging = Bar(response.Body.Metadata, pageNumber, pageSize, records.Count);
            page.Reports = records.Select(record =>
                FacilityViewService.ToReportRow(record, null, null)).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report list failed. FacilityId={FacilityId}", facilityId.Sanitize());
            page.LoadError = "Report service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ReportsListModel> LoadForFacilitiesAsync(
        ReportsListQuery query,
        IReadOnlyList<string> facilityIds,
        bool truncated,
        CancellationToken cancellationToken)
    {
        query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
        var pageSize = FacilityViewRules.ClampPageSize(query.PageSize);
        var pageNumber = FacilityViewRules.ClampPage(query.Page);
        var sortBy = FacilityViewRules.AllowedSort(query.SortBy, FacilityViewRules.ReportSorts) ?? "CreateDate";
        var sortDir = FacilityViewRules.ParseSortDirection(query.SortDir) == SortOrder.Ascending ? "asc" : "desc";
        var cached = await AutomationFacilitySearch.CachedAsync(
            _counts,
            "reports",
            query.AutomationFingerprint(),
            facilityIds,
            async (facilityId, token) =>
            {
                var one = await LoadListAsync(query.WithFacility(facilityId, 1, FacilityViewRules.PageSizes[^1]), token);
                return new FacilitySearchPage<FacilityReportRow>(one.Reports, one.Paging.TotalCount, one.LoadError);
            },
            cancellationToken);

        var slice = AutomationMarkRules.Slice(cached.Rows, pageNumber, pageSize);
        return new ReportsListModel
        {
            Query = query,
            LoadError = cached.Error,
            Reports = slice.Items,
            Paging = new PageBar
            {
                Page = slice.Page,
                PageSize = slice.Size,
                TotalCount = slice.Total,
                TotalPages = slice.Pages
            },
            SortBy = sortBy,
            SortDir = sortDir,
            ScopeNote = AutomationMarkRules.SearchNote(truncated, cached.Partial)
        };
    }

    public async Task<GenerateReportPage> LoadGenerateAsync(CancellationToken cancellationToken)
    {
        var page = new GenerateReportPage
        {
            AutomationFacilitiesExcluded = _options.AutomationEnabled
        };
        if (_measure is null)
        {
            page.MeasuresNote = MeasureNotConfigured + " Type the measure ids below.";
            return page;
        }

        try
        {
            var response = await _measure.GetAllMeasureDefinitionsAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                page.MeasuresNote = FacilityFormRules.ServiceMessage("MeasureEval", response.StatusCode, response.RawBody)
                    + " Type the measure ids below.";
                return page;
            }

            page.Measures = ReportsRules.MeasureIds(response.Body);
            if (page.Measures.Count == 0)
                page.MeasuresNote = "MeasureEval has no measure definitions. Type the measure ids below.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Measure definition list failed");
            page.MeasuresNote = "MeasureEval call failed: " + ex.Message + " Type the measure ids below.";
        }

        return page;
    }

    public async Task<GenerateReportPage> GenerateAsync(GenerateReportInput? input, CancellationToken cancellationToken)
    {
        input ??= new GenerateReportInput();
        var page = await LoadGenerateAsync(cancellationToken);
        page.Input = input;

        var facilityId = FacilityViewRules.Clean(input.FacilityId);
        if (!FacilityFormRules.IsValidFacilityId(facilityId, _options.NumericOnlyFacilityId))
        {
            page.Error = FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId);
            return page;
        }

        var gate = await OwnershipGateAsync(facilityId, cancellationToken);
        if (gate.Unreachable)
        {
            page.Error = AutomationMarkRules.OwnershipUnreachable;
            return page;
        }

        if (gate.Blocked)
        {
            page.Error = AutomationMarkRules.AdHocReportBlocked;
            return page;
        }

        var types = ReportsRules.ReportTypes(input.ReportTypes, input.ReportTypesText);
        if (types.Count == 0)
        {
            page.Error = "Choose at least one measure.";
            return page;
        }

        var census = string.Equals(input.PatientsMode, "census", StringComparison.OrdinalIgnoreCase);
        var patients = ReportsRules.SplitIds(input.Patients);
        if (!census && patients.Count == 0)
        {
            page.Error = "Enter at least one patient, or choose the facility census.";
            return page;
        }

        try
        {
            var facility = await _facilities.GetAsync(facilityId!, cancellationToken);
            if (facility.StatusCode == StatusCodes.Status404NotFound)
            {
                page.Error = "That facility was not found.";
                return page;
            }

            if (!facility.IsSuccessStatusCode || facility.Body is null)
            {
                page.Error = FacilityFormRules.ServiceMessage("Tenant", facility.StatusCode, facility.RawBody);
                return page;
            }

            if (!ReportsRules.TryReportingPeriod(
                    input.Cadence,
                    FacilityViewRules.ParseDate(input.StartDate),
                    FacilityViewRules.ParseDate(input.EndDate),
                    facility.Body.TimeZone,
                    out var startUtc,
                    out var endUtc,
                    out var periodError))
            {
                page.Error = periodError;
                return page;
            }

            var response = await _facilities.GenerateAdhocReportAsync(facilityId!, new AdHocReportRequest
            {
                BypassSubmission = input.BypassSubmission,
                StartDate = startUtc,
                EndDate = endUtc,
                ReportTypes = types.ToList(),
                PatientIds = census ? null : patients.ToList()
            }, cancellationToken);

            if (!response.IsSuccessStatusCode || response.Body is null || response.Body.ReportId == Guid.Empty)
            {
                page.Error = FacilityFormRules.ServiceMessage("Tenant", response.StatusCode, response.RawBody);
                return page;
            }

            page.Error = null;
            page.Input.FacilityId = facilityId;
            page.GeneratedReportId = response.Body.ReportId;
            return page;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ad-hoc report failed. FacilityId={FacilityId}", facilityId.Sanitize());
            page.Error = "Tenant service call failed: " + ex.Message;
            return page;
        }
    }

    public async Task<ReportsAction> CopyDownloadAsync(
        string? facilityId,
        string? reportId,
        Stream destination,
        CancellationToken cancellationToken)
    {
        try
        {
            var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
            if (opened.Page.LoadError is not null || opened.Page.NotFound || opened.Schedule is null)
                return Fail(opened.Page.LoadError ?? "That report was not found.");

            if (!ReportsRules.CanDownload(opened.Schedule.IsDeleted == true, opened.Schedule.PayloadRootUri))
                return Fail("This report has no package to download.");

            if (_submission is null)
                return Fail(SubmissionNotConfigured);

            var copy = await _submission.CopySubmissionAsync(
                opened.Page.FacilityId!,
                opened.Page.ReportId,
                destination,
                external: false,
                cancellationToken);
            return copy.StatusCode is >= 200 and < 300
                ? new ReportsAction(true, string.Empty)
                : Fail(FacilityFormRules.ServiceMessage("Submission", copy.StatusCode, copy.ErrorBody));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report download failed");
            return Fail("Submission service call failed: " + ex.Message);
        }
    }

    public async Task<ValidationPage> LoadValidationPageAsync(
        string? facilityId,
        string? reportId,
        ValidationIssueQuery query,
        CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        var page = Copy<ValidationPage>(opened.Page);
        page.Query = query;
        if (page.LoadError is not null || page.NotFound)
            return page;

        if (_validation is null)
        {
            page.SummaryError = ValidationNotConfigured;
            page.IssuesError = ValidationNotConfigured;
            return page;
        }

        await ReadSummaryAsync(page, cancellationToken);
        await ReadIssuesAsync(page, cancellationToken);
        return page;
    }

    public async Task<MeasureReportPage> LoadMeasureAsync(
        string? facilityId,
        string? reportId,
        string? patientId,
        bool showJson,
        CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        var page = Copy<MeasureReportPage>(opened.Page);
        page.PatientId = FacilityViewRules.Clean(patientId);
        page.ShowJson = showJson;
        if (page.LoadError is not null || page.NotFound)
            return page;

        if (page.PatientId is null)
        {
            page.LoadError = "Patient ID is required.";
            return page;
        }

        if (_reports is null)
        {
            page.LoadError = ReportNotConfigured;
            return page;
        }

        try
        {
            var entry = await _reports.GetEntryByScheduleAndPatientAsync(page.ReportId, page.PatientId, cancellationToken);
            if (entry.StatusCode == StatusCodes.Status404NotFound)
            {
                page.NotFound = true;
                page.LoadError = "That patient is not on this report.";
                return page;
            }

            if (!entry.IsSuccessStatusCode || entry.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Report", entry.StatusCode, entry.RawBody);
                return page;
            }

            page.ReportingStatus = ReportsRules.ReportingLabel(entry.Body.ReportingStatus.ToString());
            page.SubmissionStatus = ReportsRules.SubmissionLabel(entry.Body.SubmissionStatus?.ToString());
            page.Measures = (entry.Body.MeasureReports ?? []).Select(measure => new MeasureReportLine
            {
                Id = measure.MeasureReportId,
                Type = measure.ReportType,
                Status = measure.Status?.ToString() ?? "",
                Resources = measure.ResourceCount is not { Count: > 0 }
                    ? "—"
                    : string.Join(", ", measure.ResourceCount.Select(pair => $"{pair.Key} {pair.Value}"))
            }).ToList();
            page.CanDownloadJson = _measure is not null;

            if (!showJson)
                return page;

            if (_measure is null)
            {
                page.JsonError = MeasureNotConfigured;
                return page;
            }

            var bundle = await _measure.GetPatientBundleAsync(page.FacilityId!, page.ReportId, page.PatientId, cancellationToken);
            if (bundle.StatusCode == StatusCodes.Status404NotFound)
            {
                page.JsonError = "MeasureEval has no input bundle for this patient.";
                return page;
            }

            if (!bundle.IsSuccessStatusCode)
            {
                page.JsonError = FacilityFormRules.ServiceMessage("MeasureEval", bundle.StatusCode, bundle.RawBody);
                return page;
            }

            page.Json = ReportsRules.PrettyJson(bundle.Body, out var tooLarge);
            page.JsonTooLarge = tooLarge;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Measure report failed. ReportId={ReportId}", page.ReportId.Sanitize());
            page.LoadError = "Report service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<(ReportsAction Action, string? Body, string? PatientId)> ReadPatientBundleAsync(
        string? facilityId,
        string? reportId,
        string? patientId,
        CancellationToken cancellationToken)
    {
        var page = await LoadMeasureAsync(facilityId, reportId, patientId, showJson: false, cancellationToken);
        if (page.LoadError is not null || page.NotFound)
            return (Fail(page.LoadError ?? "That patient is not on this report."), null, page.PatientId);

        if (_measure is null)
            return (Fail(MeasureNotConfigured), null, page.PatientId);

        var bundle = await _measure.GetPatientBundleAsync(page.FacilityId!, page.ReportId, page.PatientId!, cancellationToken);
        if (!bundle.IsSuccessStatusCode || string.IsNullOrWhiteSpace(bundle.Body))
            return (Fail(FacilityFormRules.ServiceMessage("MeasureEval", bundle.StatusCode, bundle.RawBody)), null, page.PatientId);

        return (new ReportsAction(true, string.Empty), bundle.Body, page.PatientId);
    }

    public async Task<ReportSectionPage> LoadSectionAsync(
        string? facilityId,
        string? reportId,
        CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        return opened.Page;
    }

    private async Task ReadSummaryAsync(ValidationPage page, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _validation!.GetValidationResultSummaryAsync(
                page.FacilityId!,
                page.ReportId,
                ReportsRules.InformationSeverity,
                cancellationToken);
            if (summary.StatusCode == StatusCodes.Status404NotFound)
            {
                page.IssueCount = 0;
                page.Severity = ReportsRules.InformationSeverity;
                return;
            }

            if (!summary.IsSuccessStatusCode)
            {
                page.SummaryError = FacilityFormRules.ServiceMessage("Validation", summary.StatusCode, summary.RawBody);
                return;
            }

            if (!ReportsRules.TryParseResultSummary(summary.Body, out var count, out var severity))
            {
                page.SummaryError = "Validation returned a summary this page could not read.";
                return;
            }

            page.IssueCount = count;
            page.Severity = string.IsNullOrWhiteSpace(severity) ? ReportsRules.InformationSeverity : severity;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation summary failed. ReportId={ReportId}", page.ReportId.Sanitize());
            page.SummaryError = "Validation service call failed: " + ex.Message;
        }
    }

    private async Task ReadIssuesAsync(ValidationPage page, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _validation!.GetValidationResultsAsync(
                page.FacilityId!,
                page.ReportId,
                ReportsRules.InformationSeverity,
                cancellationToken);
            if (results.StatusCode == StatusCodes.Status404NotFound)
            {
                ApplyIssueSlice(page, ReportsRules.SliceIssues([], page.Query), "—");
                return;
            }

            if (!results.IsSuccessStatusCode)
            {
                page.IssuesError = FacilityFormRules.ServiceMessage("Validation", results.StatusCode, results.RawBody);
                return;
            }

            if (results.Body is { Length: > ReportsRules.MaxResultChars })
            {
                page.TooLarge = true;
                page.IssuesError = "Validation results for this report are too large to list here.";
                return;
            }

            var issues = ReportsRules.ParseIssues(results.Body);
            ApplyIssueSlice(page, ReportsRules.SliceIssues(issues, page.Query), ReportsRules.IssueStanding(issues));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation issues failed. ReportId={ReportId}", page.ReportId.Sanitize());
            page.IssuesError = "Validation service call failed: " + ex.Message;
        }
    }

    private static void ApplyIssueSlice(ValidationPage page, IssueSlice slice, string standing)
    {
        page.PrequalStatus = standing;
        page.SeverityCounts = slice.SeverityCounts;
        page.SeverityOptions = slice.Severities;
        page.CategoryOptions = slice.Categories;
        page.Issues = slice.Page;
        page.Paging = new PageBar
        {
            Page = slice.PageNumber,
            PageSize = page.Query.PageSize,
            TotalCount = slice.Total,
            TotalPages = slice.TotalPages
        };
    }

    public async Task<ReportManifestPage> LoadManifestAsync(
        string? facilityId,
        string? reportId,
        ReportManifestQuery query,
        string path,
        IReadOnlyDictionary<string, string> route,
        Func<string, string?> patientHref,
        CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        var page = new ReportManifestPage
        {
            FacilityId = opened.Page.FacilityId,
            FacilityName = opened.Page.FacilityName,
            ReportId = opened.Page.ReportId,
            NotFound = opened.Page.NotFound,
            LoadError = opened.Page.LoadError,
            Report = opened.Page.Report
        };
        if (page.LoadError is not null || page.NotFound || opened.Schedule is null || _reports is null)
            return page;

        var scheduleId = opened.Schedule.Id.ToString();
        var contents = new List<ManifestCountRow>();
        var populationRows = new List<(string Measure, string PopulationId, int Count)>();
        var slots = new List<PopulationSlot>();
        var heading = "Populations";
        string? totalLabel = null;
        try
        {
            var populations = await _reports.GetPopulationsByScheduleAsync(scheduleId, cancellationToken: cancellationToken);
            if (populations.IsSuccessStatusCode && populations.Body is not null)
            {
                foreach (var population in populations.Body)
                {
                    var measure = string.IsNullOrWhiteSpace(population.Measure) ? population.ReportType : population.Measure;
                    if (string.IsNullOrWhiteSpace(measure))
                        continue;

                    foreach (var group in population.GroupPopulations ?? [])
                    {
                        var label = ReportManifestRules.PopulationLabel(group.PopulationId);
                        var name = string.IsNullOrWhiteSpace(label) ? measure.Trim() : measure.Trim() + " / " + label;
                        contents.Add(new ManifestCountRow
                        {
                            Name = name,
                            Primary = group.TotalPopulationCount,
                            Total = group.TotalPopulationCount
                        });
                        populationRows.Add((measure.Trim(), group.PopulationId, group.TotalPopulationCount));
                        slots.Add(new PopulationSlot
                        {
                            Measure = measure.Trim(),
                            PopulationId = group.PopulationId,
                            MeasureReportIds = (group.MeasureReportPopulations ?? [])
                                .Select(item => item.MeasureReportId)
                                .Where(id => !string.IsNullOrWhiteSpace(id))
                                .Select(id => id.Trim())
                                .ToList()
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report populations failed. ReportId={ReportId}", page.ReportId.Sanitize());
        }

        int? passedValidation = null;
        int? failedValidation = null;
        int? pendingValidation = null;
        try
        {
            var summary = await _reports.GetEntrySummaryByScheduleAsync(scheduleId, cancellationToken);
            if (summary.IsSuccessStatusCode && summary.Body is not null)
            {
                var counts = summary.Body.ReportingStatusCounts ?? new Dictionary<string, int>();
                passedValidation = StatusCount(counts, "PassedValidation");
                failedValidation = StatusCount(counts, "FailedValidation");
                pendingValidation = StatusCount(counts, "PendingValidation");
                if (contents.Count == 0)
                {
                    heading = "Patients by status";
                    totalLabel = "Patients";
                    foreach (var pair in counts)
                    {
                        contents.Add(new ManifestCountRow
                        {
                            Name = ReportManifestRules.Words(pair.Key),
                            Primary = pair.Value,
                            Total = pair.Value
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report entry summary failed. ReportId={ReportId}", page.ReportId.Sanitize());
        }

        var sortBy = query.Sort == "id" ? "patientid" : "reportingstatus";
        var sortOrder = query.Descending ? SortOrder.Descending : SortOrder.Ascending;
        IReadOnlyList<ManifestPatientRow> patients = [];
        var patientBar = new PageBar { Page = query.Page, PageSize = query.PageSize };
        string? patientNote = null;
        try
        {
            var entries = await _reports.SearchEntriesAsync(
                page.FacilityId,
                query.PatientQuery,
                scheduleId,
                sortBy: sortBy,
                pageSize: query.PageSize,
                pageNumber: query.Page,
                sortOrder: sortOrder,
                cancellationToken: cancellationToken);
            if (!entries.IsSuccessStatusCode || entries.Body is null)
            {
                patientNote = FacilityFormRules.ServiceMessage("Report", entries.StatusCode, entries.RawBody);
            }
            else
            {
                patients = entries.Body.Records.Select(entry =>
                {
                    var counts = entry.MeasureReports
                        .SelectMany(report => report.ResourceCount ?? [])
                        .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => (Name: group.Key, Count: group.Sum(pair => pair.Value)))
                        .OrderByDescending(row => row.Count)
                        .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var total = counts.Sum(row => row.Count);
                    var shown = counts.Take(4).Select(row => row.Name + " " + row.Count.ToString("N0")).ToList();
                    var extra = counts.Count - shown.Count;
                    var detail = shown.Count == 0 ? null : string.Join(", ", shown) + (extra > 0 ? " +" + extra : "");
                    var reportIds = (entry.MeasureReports ?? [])
                        .Select(report => report.MeasureReportId)
                        .ToList();
                    var updated = ReportManifestRules.When(entry.ModifyDate) ?? ReportManifestRules.When(entry.CreateDate);
                    var events = new List<ManifestTimelineEvent>();
                    AddEvent(events, "Identified", entry.CreateDate);
                    AddEvent(events, "Acquisition evaluated", entry.AcquisitionEvaluatedAt);
                    AddEvent(events, "Normalization evaluated", entry.NormalizationEvaluatedAt);
                    if (entry.ModifyDate is not null && entry.ModifyDate != entry.CreateDate)
                        AddEvent(events, "Last updated", entry.ModifyDate);
                    return ReportManifestRules.PatientRow(
                        entry.PatientId,
                        entry.ReportingStatus.ToString(),
                        entry.SubmissionStatus?.ToString(),
                        total,
                        detail,
                        patientHref(entry.PatientId),
                        updated,
                        ReportManifestRules.ReportBadges(reportIds, slots),
                        ReportManifestRules.ResourceCounts(counts.Select(row => new KeyValuePair<string, int>(row.Name, row.Count))),
                        events,
                        PatientLinks(entry.PatientId, patientHref(entry.PatientId), page.FacilityId, page.ReportId),
                        membership: ReportManifestRules.MembershipFor(reportIds, slots));
                }).ToList();
                var metadata = entries.Body.Metadata;
                patientBar = new PageBar
                {
                    Page = metadata?.PageNumber > 0 ? metadata.PageNumber : query.Page,
                    PageSize = metadata?.PageSize > 0 ? metadata.PageSize : query.PageSize,
                    TotalCount = metadata?.TotalCount ?? patients.Count,
                    TotalPages = metadata?.TotalPages ?? (patients.Count == 0 ? 0 : 1)
                };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report manifest patients failed. ReportId={ReportId}", page.ReportId.Sanitize());
            patientNote = "Report service call failed.";
        }

        var patientCount = (int)patientBar.TotalCount;
        if (string.IsNullOrWhiteSpace(query.PatientQuery))
        {
            try
            {
                var counted = await _reports.GetEntryCountByScheduleAsync(scheduleId, cancellationToken);
                if (counted.IsSuccessStatusCode)
                    patientCount = counted.Body;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report entry count failed. ReportId={ReportId}", page.ReportId.Sanitize());
            }
        }

        var measures = opened.Schedule.ReportTypes ?? [];
        var highlights = ReportManifestRules.Highlights(populationRows);
        var members = ReportManifestRules.PopulationPage(
            patients,
            query.Stage,
            query.StageMeasure,
            query.PopulationPage,
            query.PageSize,
            ReportManifestRules.StageTotal(highlights, query.StageMeasure, query.Stage));
        page.Manifest = ReportManifestRules.FromReport(
            new ReportManifestFacts
            {
                PatientCount = patientCount,
                InitialPopulation = page.Report?.InitialPopulationCount ?? 0,
                ContentTotal = contents.Sum(row => row.Total),
                Measures = measures,
                Contents = contents,
                ContentsHeading = heading,
                TotalLabel = totalLabel,
                Populations = highlights,
                PassedValidation = passedValidation,
                FailedValidation = failedValidation,
                PendingValidation = pendingValidation,
                Patients = patients,
                PatientPaging = patientBar,
                PatientNote = patientNote,
                PopulationPatients = members.Page,
                PopulationPaging = members.Bar,
                PopulationNote = members.Note
            },
            query,
            path,
            route);
        return page;
    }

    private async Task<(ReportSectionPage Page, ReportScheduleApiModel? Schedule)> OpenReportAsync(
        string? facilityId,
        string? reportId,
        CancellationToken cancellationToken)
    {
        var page = new ReportSectionPage
        {
            FacilityId = facilityId?.Trim(),
            ReportId = reportId?.Trim() ?? string.Empty
        };

        if (!FacilityFormRules.IsValidFacilityId(page.FacilityId, _options.NumericOnlyFacilityId))
        {
            page.NotFound = true;
            page.LoadError = FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId);
            return (page, null);
        }

        var parsed = FacilityViewRules.ParseReportId(page.ReportId, out var idError);
        if (parsed is null)
        {
            page.NotFound = true;
            page.LoadError = idError ?? "Report ID is required.";
            return (page, null);
        }

        page.ReportId = parsed.Value.ToString();
        if (_reports is null)
        {
            page.LoadError = ReportNotConfigured;
            return (page, null);
        }

        var facility = await _facilities.GetAsync(page.FacilityId!, cancellationToken);
        if (facility.StatusCode == StatusCodes.Status404NotFound)
        {
            page.NotFound = true;
            page.LoadError = "That facility was not found.";
            return (page, null);
        }

        if (!facility.IsSuccessStatusCode || facility.Body is null)
        {
            page.LoadError = FacilityFormRules.ServiceMessage("Tenant", facility.StatusCode, facility.RawBody);
            return (page, null);
        }

        page.FacilityName = facility.Body.FacilityName;
        var schedule = await _reports.GetScheduleAsync(page.ReportId, cancellationToken);
        if (schedule.StatusCode == StatusCodes.Status404NotFound)
        {
            page.NotFound = true;
            page.LoadError = "That report was not found.";
            return (page, null);
        }

        if (!schedule.IsSuccessStatusCode || schedule.Body is null)
        {
            page.LoadError = FacilityFormRules.ServiceMessage("Report", schedule.StatusCode, schedule.RawBody);
            return (page, null);
        }

        if (!string.Equals(schedule.Body.FacilityId, page.FacilityId, StringComparison.Ordinal))
        {
            page.NotFound = true;
            page.LoadError = "That report is not for this facility.";
            return (page, null);
        }

        var counts = await CountsAsync(schedule.Body.Id, cancellationToken);
        page.Report = FacilityViewService.ToReportRow(schedule.Body, counts.Census, counts.Population);
        return (page, schedule.Body);
    }

    public async Task<ReportScheduleApiModel?> TryScheduleAsync(string? facilityId, string? reportId, CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        return opened.Page.LoadError is null ? opened.Schedule : null;
    }

    public async Task<IReadOnlyList<ReportCountRow>> LoadCountsAsync(string? ids, CancellationToken cancellationToken)
    {
        var parsed = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(FacilityViewRules.PageSizes.Max())
            .ToList();
        if (parsed.Count == 0 || _reports is null)
            return Array.Empty<ReportCountRow>();

        var rows = new ReportCountRow[parsed.Count];
        var pending = new List<Task>();
        for (var index = 0; index < parsed.Count; index++)
        {
            var id = parsed[index];
            var slot = index;
            var key = "report-counts:" + id.ToString("N");
            if (_counts.TryGetValue(key, out (int? Census, int? Population) cached))
            {
                rows[slot] = new ReportCountRow(id.ToString(), cached.Census, cached.Population);
                continue;
            }

            pending.Add(LoadOne());

            async Task LoadOne()
            {
                var counts = await CountsAsync(id, cancellationToken);
                _counts.Set(key, counts, TimeSpan.FromSeconds(15));
                rows[slot] = new ReportCountRow(id.ToString(), counts.Census, counts.Population);
            }
        }

        await Task.WhenAll(pending);
        return rows;
    }

    private async Task<(int? Census, int? Population)> CountsAsync(Guid reportId, CancellationToken cancellationToken)
    {
        if (_reports is null)
            return (null, null);

        var id = reportId.ToString();
        var census = _reports.GetEntryCountByScheduleAsync(id, cancellationToken);
        var population = _reports.GetInitialPopulationCountAsync(id, cancellationToken);
        await Task.WhenAll(census, population);
        return (
            (await census).IsSuccessStatusCode ? (await census).Body : null,
            (await population).IsSuccessStatusCode ? (await population).Body : null);
    }

    private static PageBar Bar(PaginationMetadata? metadata, int page, int pageSize, int recordCount)
    {
        if (metadata is null)
        {
            return new PageBar
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = recordCount,
                TotalPages = recordCount == 0 ? 0 : 1
            };
        }

        return new PageBar
        {
            Page = metadata.PageNumber < 1 ? page : metadata.PageNumber,
            PageSize = metadata.PageSize < 1 ? pageSize : metadata.PageSize,
            TotalCount = metadata.TotalCount,
            TotalPages = metadata.TotalPages
        };
    }

    private static T Copy<T>(ReportSectionPage source) where T : ReportSectionPage, new() => new()
    {
        FacilityId = source.FacilityId,
        FacilityName = source.FacilityName,
        ReportId = source.ReportId,
        NotFound = source.NotFound,
        LoadError = source.LoadError,
        Report = source.Report
    };

    private readonly record struct OwnershipGate(bool Blocked, bool Unreachable);

    private async Task<OwnershipGate> OwnershipGateAsync(string? facilityId, CancellationToken cancellationToken)
    {
        if (!_options.AutomationEnabled || _ownership is null)
            return new(false, false);

        var (index, reachable) = await _ownership(cancellationToken);
        if (!reachable)
            return new(true, true);
        return new(index.Contains(facilityId), false);
    }

    private static ReportsAction Fail(string message) => new(false, message);

    private static int StatusCount(IReadOnlyDictionary<string, int> counts, string name)
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

    private static void AddEvent(List<ManifestTimelineEvent> events, string label, DateTime? when)
    {
        var text = ReportManifestRules.When(when);
        if (text is not null)
            events.Add(new ManifestTimelineEvent { Label = label, When = text });
    }

    private static List<ManifestLink> PatientLinks(string patientId, string? measureHref, string? facilityId, string? reportId)
    {
        var links = new List<ManifestLink>();
        if (!string.IsNullOrWhiteSpace(measureHref))
            links.Add(new ManifestLink { Label = "Measure report", Href = measureHref });

        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(patientId))
            return links;

        var facility = Uri.EscapeDataString(facilityId.Trim());
        var patient = Uri.EscapeDataString(patientId.Trim());
        var report = string.IsNullOrWhiteSpace(reportId) ? string.Empty : "&reportId=" + Uri.EscapeDataString(reportId.Trim());
        links.Add(new ManifestLink
        {
            Label = "Acquisition log",
            Href = "/Logs/Acquisition?facilityId=" + facility + "&patientId=" + patient + report
        });
        links.Add(new ManifestLink
        {
            Label = "Audit log",
            Href = "/Logs/Audit?facilityId=" + facility + "&searchText=" + patient
        });
        return links;
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
