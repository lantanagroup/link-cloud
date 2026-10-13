using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Automation.Generation;
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
    private readonly ReportManifestArchive? _archive;

    public ReportsService(
        IFacilityServiceClient facilities,
        IReportServiceClient? reports,
        ISubmissionServiceClient? submission,
        IValidationServiceClient? validation,
        IMeasureEvalServiceClient? measure,
        IOptions<LinkUiFeatureOptions> options,
        IMemoryCache counts,
        ILogger<ReportsService> logger,
        ReportManifestArchive? archive = null)
    {
        _archive = archive;
        _facilities = facilities;
        _reports = reports;
        _submission = submission;
        _validation = validation;
        _measure = measure;
        _options = options.Value;
        _counts = counts;
        _logger = logger;
    }

    public static ReportsService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        return new ReportsService(
            services.GetRequiredService<IFacilityServiceClient>(),
            Blank(registry.ReportServiceUrl) ? null : services.GetRequiredService<IReportServiceClient>(),
            Blank(registry.SubmissionServiceUrl) ? null : services.GetRequiredService<ISubmissionServiceClient>(),
            Blank(registry.ValidationServiceUrl) ? null : services.GetRequiredService<IValidationServiceClient>(),
            Blank(registry.MeasureServiceUrl) ? null : services.GetRequiredService<IMeasureEvalServiceClient>(),
            services.GetRequiredService<IOptions<LinkUiFeatureOptions>>(),
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<ILogger<ReportsService>>(),
            ReportManifestArchive.From(services));
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

        LinkApiResponse<FacilityModel> facility;
        try
        {
            facility = await _facilities.GetAsync(facilityId!, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Facility test flag could not be read. FacilityId={FacilityId}", facilityId.Sanitize());
            page.Error = AutomationMarkRules.OwnershipUnreachable;
            return page;
        }

        if (facility.StatusCode == StatusCodes.Status404NotFound)
        {
            page.Error = "That facility was not found.";
            return page;
        }

        if (!facility.IsSuccessStatusCode || facility.Body is null)
        {
            page.Error = AutomationMarkRules.OwnershipUnreachable;
            return page;
        }

        if (facility.Body.IsTest)
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

        var saved = await ReadSavedManifestAsync(page.FacilityId, page.ReportId, cancellationToken);
        var live = opened.Schedule is not null && _reports is not null && page.LoadError is null && !page.NotFound;
        if (saved is null)
        {
            if (!live)
                return page;
            saved = await BuildManifestSectionAsync(page.FacilityId!, page.FacilityName, opened.Schedule!, null, cancellationToken);
            if (saved is null)
            {
                page.LoadError = "The report manifest could not be built from the Report service.";
                return page;
            }

            // Only a finished report is saved; an open one keeps changing.
            if (opened.Schedule!.EndOfReportPeriodJobHasRun)
                await SaveManifestAsync(saved, cancellationToken);
        }

        string? notice = null;
        if (!live)
        {
            page.NotFound = false;
            page.LoadError = null;
            page.FacilityName ??= saved.FacilityName;
            notice = "Facility deleted. This is the manifest saved on " + saved.BuiltAt.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture)
                + (string.IsNullOrWhiteSpace(saved.FacilityName) || saved.FacilityName == saved.FacilityId ? "." : " for " + saved.FacilityName + ".");
        }

        page.Manifest = RenderManifest(saved, query, path, route, live ? patientHref : null, notice);
        return page;
    }

    /// <summary>Builds and saves the report manifest. Automation calls this at the end of a run, before cleanup.</summary>
    public async Task<bool> SaveReportManifestAsync(string facilityId, string reportId, Guid? runId, CancellationToken cancellationToken)
    {
        if (_archive is null || _reports is null)
            return false;
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        if (opened.Schedule is null)
            return false;
        var section = await BuildManifestSectionAsync(opened.Page.FacilityId!, opened.Page.FacilityName, opened.Schedule, runId, cancellationToken);
        if (section is null)
            return false;
        await SaveManifestAsync(section, cancellationToken);
        return true;
    }

    private async Task<ReportManifestSection?> ReadSavedManifestAsync(string? facilityId, string? reportId, CancellationToken cancellationToken)
    {
        if (_archive is null || !Guid.TryParse(reportId, out var id) || string.IsNullOrWhiteSpace(facilityId))
            return null;
        var cacheKey = "manifest|" + id.ToString("D");
        if (_counts.TryGetValue(cacheKey, out ReportManifestSection? cached) && cached is not null)
            return string.Equals(cached.FacilityId, facilityId.Trim(), StringComparison.Ordinal) ? cached : null;
        try
        {
            var section = await _archive.ReadAsync(id, cancellationToken);
            if (section is null)
                return null;
            _counts.Set(cacheKey, section, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), Size = 1 });
            return string.Equals(section.FacilityId, facilityId.Trim(), StringComparison.Ordinal) ? section : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saved report manifest read failed. ReportId={ReportId}", reportId.Sanitize());
            return null;
        }
    }

    private async Task SaveManifestAsync(ReportManifestSection section, CancellationToken cancellationToken)
    {
        if (_archive is null)
            return;
        try
        {
            await _archive.SaveAsync(section, cancellationToken);
            _counts.Remove("manifest|" + section.ReportId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report manifest save failed. ReportId={ReportId}", section.ReportId.Sanitize());
        }
    }

    /// <summary>Reads every entry page once and keeps ids, counts and times per patient. No resource bodies are kept.</summary>
    private async Task<ReportManifestSection?> BuildManifestSectionAsync(
        string facilityId,
        string? facilityName,
        ReportScheduleApiModel schedule,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        if (_reports is null)
            return null;
        var scheduleId = schedule.Id.ToString();
        var populations = new List<ReportManifestPopulation>();
        var memberships = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var response = await _reports.GetPopulationsByScheduleAsync(scheduleId, cancellationToken: cancellationToken);
            if (response.IsSuccessStatusCode && response.Body is not null)
            {
                foreach (var population in response.Body)
                {
                    var measure = string.IsNullOrWhiteSpace(population.Measure) ? population.ReportType : population.Measure;
                    if (string.IsNullOrWhiteSpace(measure))
                        continue;
                    foreach (var group in population.GroupPopulations ?? [])
                    {
                        var key = measure.Trim() + "|" + group.PopulationId;
                        populations.Add(new ReportManifestPopulation { Measure = measure.Trim(), PopulationId = group.PopulationId, Count = group.TotalPopulationCount });
                        foreach (var item in group.MeasureReportPopulations ?? [])
                        {
                            if (string.IsNullOrWhiteSpace(item.MeasureReportId))
                                continue;
                            var reportKey = item.MeasureReportId.Trim();
                            if (!memberships.TryGetValue(reportKey, out var keys))
                                memberships[reportKey] = keys = [];
                            if (!keys.Contains(key))
                                keys.Add(key);
                        }
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
            _logger.LogError(ex, "Report populations failed. ReportId={ReportId}", scheduleId.Sanitize());
        }

        IReadOnlyDictionary<string, int> statusCounts = new Dictionary<string, int>();
        IReadOnlyDictionary<string, int> typeCounts = new Dictionary<string, int>();
        try
        {
            var summary = await _reports.GetEntrySummaryByScheduleAsync(scheduleId, cancellationToken);
            if (summary.IsSuccessStatusCode && summary.Body is not null)
            {
                statusCounts = summary.Body.ReportingStatusCounts ?? new Dictionary<string, int>();
                typeCounts = summary.Body.ResourceTypeCounts ?? new Dictionary<string, int>();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report entry summary failed. ReportId={ReportId}", scheduleId.Sanitize());
        }

        var patients = new List<ReportManifestPatient>();
        var pageNumber = 1;
        while (true)
        {
            var entries = await _reports.SearchEntriesAsync(
                facilityId,
                null,
                scheduleId,
                sortBy: "patientid",
                pageSize: ManifestBuildPageSize,
                pageNumber: pageNumber,
                sortOrder: SortOrder.Ascending,
                cancellationToken: cancellationToken);
            if (!entries.IsSuccessStatusCode || entries.Body is null)
            {
                _logger.LogError("Report manifest entries failed. ReportId={ReportId} Status={Status}", scheduleId.Sanitize(), entries.StatusCode);
                return null;
            }

            foreach (var entry in entries.Body.Records)
                patients.Add(ManifestPatient(entry, memberships));

            var totalPages = entries.Body.Metadata?.TotalPages ?? 1;
            if (entries.Body.Records.Count == 0 || pageNumber >= totalPages)
                break;
            pageNumber++;
        }

        return new ReportManifestSection
        {
            ReportId = scheduleId,
            FacilityId = facilityId,
            FacilityName = facilityName,
            RunId = runId,
            BuiltAt = DateTimeOffset.UtcNow,
            PatientCount = patients.Count,
            InitialPopulation = populations
                .Where(row => ReportManifestRules.PopulationKey(row.PopulationId) == "initial-population")
                .Select(row => row.Count)
                .DefaultIfEmpty(0)
                .Max(),
            Measures = schedule.ReportTypes ?? [],
            Populations = populations,
            PassedValidation = statusCounts.Count == 0 ? null : StatusCount(statusCounts, "PassedValidation"),
            FailedValidation = statusCounts.Count == 0 ? null : StatusCount(statusCounts, "FailedValidation"),
            PendingValidation = statusCounts.Count == 0 ? null : StatusCount(statusCounts, "PendingValidation"),
            StatusCounts = statusCounts,
            ResourceTypeCounts = typeCounts,
            Patients = patients
        };
    }

    private const int ManifestBuildPageSize = 500;

    private static ReportManifestPatient ManifestPatient(ReportEntryApiModel entry, Dictionary<string, List<string>> memberships)
    {
        var ids = (entry.MeasureReports ?? [])
            .Select(report => report.MeasureReportId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .ToList();
        var keys = new List<string>();
        foreach (var id in ids)
        {
            if (!memberships.TryGetValue(id, out var owned))
                continue;
            foreach (var key in owned)
                if (!keys.Contains(key))
                    keys.Add(key);
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in (entry.MeasureReports ?? []).SelectMany(report => report.ResourceCount ?? []))
            counts[pair.Key] = counts.GetValueOrDefault(pair.Key) + pair.Value;

        var timeline = new List<ReportManifestTimelineEntry>();
        void Add(string label, DateTime? at)
        {
            if (at is DateTime value)
                timeline.Add(new ReportManifestTimelineEntry { Label = label, At = value });
        }
        Add("Identified", entry.CreateDate);
        Add("Acquisition evaluated", entry.AcquisitionEvaluatedAt);
        Add("Normalization evaluated", entry.NormalizationEvaluatedAt);
        if (entry.ModifyDate is not null && entry.ModifyDate != entry.CreateDate)
            Add("Last updated", entry.ModifyDate);

        return new ReportManifestPatient
        {
            PatientId = entry.PatientId,
            ReportingStatus = entry.ReportingStatus.ToString(),
            SubmissionStatus = entry.SubmissionStatus?.ToString(),
            ResourceCounts = counts,
            MeasureReportIds = ids,
            Populations = keys,
            Timeline = timeline,
            Updated = entry.ModifyDate ?? entry.CreateDate,
            LocationOrgStatus = entry.LocationOrgStatus.ToString(),
            EncounterMappingStatus = entry.EncounterMappingStatus.ToString(),
            HslocMappingStatus = entry.HslocMappingStatus.ToString()
        };
    }

    /// <summary>Filters, sorts and pages the saved patients in memory, then builds rows for the shown page only.</summary>
    private ReportManifestModel RenderManifest(
        ReportManifestSection section,
        ReportManifestQuery query,
        string path,
        IReadOnlyDictionary<string, string> route,
        Func<string, string?>? patientHref,
        string? notice)
    {
        var live = patientHref is not null;
        var slotKeys = section.Populations.Select(row => row.Measure + "|" + row.PopulationId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var allSlots = section.Populations
            .GroupBy(row => row.Measure + "|" + row.PopulationId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var contents = section.Populations.Select(row =>
        {
            var label = ReportManifestRules.PopulationLabel(row.PopulationId);
            return new ManifestCountRow
            {
                Name = string.IsNullOrWhiteSpace(label) ? row.Measure : row.Measure + " / " + label,
                Primary = row.Count,
                Total = row.Count
            };
        }).ToList();
        var heading = "Populations";
        string? totalLabel = null;
        if (contents.Count == 0)
        {
            heading = "Patients by status";
            totalLabel = "Patients";
            contents = section.StatusCounts.Select(pair => new ManifestCountRow
            {
                Name = ReportManifestRules.Words(pair.Key),
                Primary = pair.Value,
                Total = pair.Value
            }).ToList();
        }

        ManifestPatientRow Row(ReportManifestPatient patient)
        {
            const string mine = "self";
            var slots = allSlots.Select(slot => new PopulationSlot
            {
                Measure = slot.Measure,
                PopulationId = slot.PopulationId,
                MeasureReportIds = patient.Populations.Contains(slot.Measure + "|" + slot.PopulationId, StringComparer.OrdinalIgnoreCase) ? [mine] : []
            }).ToList();
            var ownedIds = new[] { (string?)mine };
            var counts = patient.ResourceCounts
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var shown = counts.Take(4).Select(pair => pair.Key + " " + pair.Value.ToString("N0")).ToList();
            var extra = counts.Count - shown.Count;
            var detail = shown.Count == 0 ? null : string.Join(", ", shown) + (extra > 0 ? " +" + extra : "");
            var href = patientHref?.Invoke(patient.PatientId);
            return ReportManifestRules.PatientRow(
                patient.PatientId,
                patient.ReportingStatus,
                patient.SubmissionStatus,
                counts.Sum(pair => pair.Value),
                detail,
                href,
                ReportManifestRules.When(patient.Updated),
                ReportManifestRules.ReportBadges(ownedIds, slots),
                ReportManifestRules.ResourceCounts(counts),
                patient.Timeline
                    .Select(item => new ManifestTimelineEvent { Label = item.Label, When = ReportManifestRules.When(item.At) ?? string.Empty })
                    .Where(item => item.When.Length > 0)
                    .ToList(),
                live ? PatientLinks(patient.PatientId, href, section.FacilityId, section.ReportId) : [],
                membership: ReportManifestRules.MembershipFor(ownedIds, slots),
                mapping: MappingOf(patient),
                logHref: live ? PatientScopedHref("/Reports/Acquisition", section.FacilityId, section.ReportId, patient.PatientId) : null,
                diagnosticsHref: live ? PatientScopedHref("/Reports/PatientDiagnostics", section.FacilityId, section.ReportId, patient.PatientId) : null);
        }

        IEnumerable<ReportManifestPatient> filtered = section.Patients;
        if (!string.IsNullOrWhiteSpace(query.PatientQuery))
        {
            var needle = query.PatientQuery.Trim();
            filtered = filtered.Where(patient => patient.PatientId.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        filtered = query.Sort == "id"
            ? (query.Descending
                ? filtered.OrderByDescending(patient => patient.PatientId, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderBy(patient => patient.PatientId, StringComparer.OrdinalIgnoreCase))
            : (query.Descending
                ? filtered.OrderByDescending(patient => patient.ReportingStatus, StringComparer.OrdinalIgnoreCase).ThenBy(patient => patient.PatientId, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderBy(patient => patient.ReportingStatus, StringComparer.OrdinalIgnoreCase).ThenBy(patient => patient.PatientId, StringComparer.OrdinalIgnoreCase));
        var matched = filtered.ToList();
        var (patientRows, patientBar) = PageOf(matched, query.Page, query.PageSize, Row);

        var highlights = ReportManifestRules.Highlights(section.Populations.Select(row => (row.Measure, row.PopulationId, row.Count)).ToList());
        IReadOnlyList<ManifestPatientRow> populationRows = [];
        var populationBar = new PageBar { Page = 1, PageSize = query.PageSize };
        if (!string.IsNullOrWhiteSpace(query.Stage))
        {
            var members = section.Patients.Where(patient => patient.Populations.Any(key =>
            {
                var split = key.IndexOf('|');
                var measure = split < 0 ? string.Empty : key[..split];
                var populationId = split < 0 ? key : key[(split + 1)..];
                return string.Equals(ReportManifestRules.PopulationKey(populationId), query.Stage, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(query.StageMeasure) || string.Equals(measure, query.StageMeasure, StringComparison.OrdinalIgnoreCase));
            })).ToList();
            (populationRows, populationBar) = PageOf(members, query.PopulationPage, query.PageSize, Row);
        }

        return ReportManifestRules.FromReport(
            new ReportManifestFacts
            {
                PatientCount = section.PatientCount,
                InitialPopulation = section.InitialPopulation,
                ContentTotal = contents.Sum(row => row.Total),
                Measures = section.Measures,
                Contents = contents,
                ResourceTypes = ReportManifestRules.StoredResourceTypes(section.ResourceTypeCounts),
                ContentsHeading = heading,
                TotalLabel = totalLabel,
                Populations = highlights,
                PassedValidation = section.PassedValidation,
                FailedValidation = section.FailedValidation,
                PendingValidation = section.PendingValidation,
                Patients = patientRows,
                PatientPaging = patientBar,
                Notice = notice,
                PopulationPatients = populationRows,
                PopulationPaging = populationBar
            },
            query,
            path,
            route,
            interactiveBlocked: live ? null : InteractiveAvailability.ReportGone);
    }

    private static (IReadOnlyList<ManifestPatientRow> Rows, PageBar Bar) PageOf(
        IReadOnlyList<ReportManifestPatient> all,
        int page,
        int size,
        Func<ReportManifestPatient, ManifestPatientRow> row)
    {
        size = size <= 0 ? ReportManifestQuery.DefaultPageSize : size;
        var pages = (all.Count + size - 1) / size;
        var current = Math.Clamp(page, 1, Math.Max(1, pages));
        var rows = all.Skip((current - 1) * size).Take(size).Select(row).ToList();
        return (rows, new PageBar { Page = current, PageSize = size, TotalCount = all.Count, TotalPages = pages });
    }

    private static ManifestMapping? MappingOf(ReportManifestPatient patient)
    {
        if (!Enum.TryParse<MappingIndicatorStatus>(patient.LocationOrgStatus, out var location)
            || !Enum.TryParse<MappingIndicatorStatus>(patient.EncounterMappingStatus, out var encounter)
            || !Enum.TryParse<MappingIndicatorStatus>(patient.HslocMappingStatus, out var hsloc))
            return null;
        return ReportPatientDiagnostics.Mapping(location, encounter, hsloc);
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

    /// <summary>
    /// Whether the report still exists, from one schedule read and nothing else (no facility,
    /// counts or submission reads). True or false when the Report service answered; null when it
    /// could not be asked, so the caller can leave the decision to the on-demand load.
    /// </summary>
    public async Task<bool?> ReportExistsAsync(string? facilityId, string? reportId, CancellationToken cancellationToken)
    {
        if (_reports is null || string.IsNullOrWhiteSpace(facilityId) || !Guid.TryParse(reportId, out var parsed))
            return null;

        try
        {
            var schedule = await _reports.GetScheduleAsync(parsed.ToString(), cancellationToken);
            if (schedule.StatusCode == StatusCodes.Status404NotFound)
                return false;
            if (!schedule.IsSuccessStatusCode || schedule.Body is null)
                return null;
            return string.Equals(schedule.Body.FacilityId, facilityId.Trim(), StringComparison.Ordinal);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The report schedule could not be checked for Interactive mode.");
            return null;
        }
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

    /// <summary>A report- and patient-scoped page link, or null when the report or facility is unknown.</summary>
    private static string? PatientScopedHref(string path, string? facilityId, string? reportId, string patientId)
    {
        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(reportId) || string.IsNullOrWhiteSpace(patientId))
            return null;
        return path + "?facilityId=" + Uri.EscapeDataString(facilityId.Trim())
            + "&reportId=" + Uri.EscapeDataString(reportId.Trim())
            + "&patientId=" + Uri.EscapeDataString(patientId.Trim());
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
