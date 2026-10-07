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
    public const string AcquisitionNotConfigured =
        "Data acquisition service URL is not configured (ServiceRegistry:DataAcquisitionServiceUrl).";

    private readonly IFacilityServiceClient _facilities;
    private readonly IReportServiceClient? _reports;
    private readonly ISubmissionServiceClient? _submission;
    private readonly IValidationServiceClient? _validation;
    private readonly IMeasureEvalServiceClient? _measure;
    private readonly IDataAcquisitionServiceClient? _acquisition;
    private readonly LinkUiFeatureOptions _options;
    private readonly IMemoryCache _counts;
    private readonly ILogger<ReportsService> _logger;

    public ReportsService(
        IFacilityServiceClient facilities,
        IReportServiceClient? reports,
        ISubmissionServiceClient? submission,
        IValidationServiceClient? validation,
        IMeasureEvalServiceClient? measure,
        IDataAcquisitionServiceClient? acquisition,
        IOptions<LinkUiFeatureOptions> options,
        IMemoryCache counts,
        ILogger<ReportsService> logger)
    {
        _facilities = facilities;
        _reports = reports;
        _submission = submission;
        _validation = validation;
        _measure = measure;
        _acquisition = acquisition;
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
            Blank(registry.DataAcquisitionServiceUrl) ? null : services.GetRequiredService<IDataAcquisitionServiceClient>(),
            services.GetRequiredService<IOptions<LinkUiFeatureOptions>>(),
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<ILogger<ReportsService>>());
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
        var page = new GenerateReportPage();
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

    public Task<ValidationResultPage> LoadValidationAsync(string? facilityId, string? reportId, CancellationToken cancellationToken) =>
        LoadSummaryAsync(facilityId, reportId, cancellationToken);

    public async Task<PrequalPage> LoadPrequalAsync(
        string? facilityId,
        string? reportId,
        string? category,
        CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        var page = Copy<PrequalPage>(opened.Page);
        page.Category = FacilityViewRules.Clean(category);
        if (page.LoadError is not null || page.NotFound)
            return page;

        if (_validation is null)
        {
            page.LoadError = ValidationNotConfigured;
            return page;
        }

        try
        {
            var results = await _validation.GetValidationResultsAsync(
                page.FacilityId!,
                page.ReportId,
                ReportsRules.InformationSeverity,
                cancellationToken);
            if (results.StatusCode == StatusCodes.Status404NotFound)
                return page;

            if (!results.IsSuccessStatusCode)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Validation", results.StatusCode, results.RawBody);
                return page;
            }

            if (results.Body is { Length: > ReportsRules.MaxResultChars })
            {
                page.TooLarge = true;
                return page;
            }

            var issues = ReportsRules.ParseIssues(results.Body);
            var groups = ReportsRules.GroupIssues(issues);
            page.Unacceptable = groups.Where(group => !group.Acceptable).ToList();
            page.Acceptable = groups.Where(group => group.Acceptable).ToList();
            page.Issues = ReportsRules.IssuesInCategory(issues, page.Category, out var total);
            page.IssueTotal = total;
            page.IssuesTruncated = total > page.Issues.Count;

            if (issues.Count > 0 && !string.IsNullOrWhiteSpace(results.Body))
            {
                var categorized = await _validation.CategorizeResultsAsync(results.Body, summarize: true, cancellationToken);
                if (!categorized.IsSuccessStatusCode)
                    page.CategorizeNote = FacilityFormRules.ServiceMessage("Validation", categorized.StatusCode, categorized.RawBody);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prequalification failed. ReportId={ReportId}", page.ReportId.Sanitize());
            page.LoadError = "Validation service call failed: " + ex.Message;
        }

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

    public async Task<AcquisitionLogPage> LoadAcquisitionAsync(
        string? facilityId,
        string? reportId,
        string? patientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        var page = Copy<AcquisitionLogPage>(opened.Page);
        page.PatientId = FacilityViewRules.Clean(patientId);
        page.PageSize = ReportsRules.ClampLogPageSize(pageSize);
        var number = FacilityViewRules.ClampPage(pageNumber);
        if (page.LoadError is not null || page.NotFound)
            return page;

        if (_acquisition is null)
        {
            page.SectionError = AcquisitionNotConfigured;
            return page;
        }

        try
        {
            var response = await _acquisition.SearchAcquisitionLogsAsync(
                page.FacilityId!,
                page.ReportId,
                pageSize: page.PageSize,
                pageNumber: number,
                sortBy: "ExecutionDate",
                sortOrder: nameof(SortOrder.Descending),
                cancellationToken: cancellationToken,
                patientId: page.PatientId);

            if (response.StatusCode == StatusCodes.Status204NoContent)
            {
                page.Paging = new PageBar { Page = 1, PageSize = page.PageSize };
                return page;
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.SectionError = FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
                return page;
            }

            page.Paging = Bar(response.Body.Metadata, number, page.PageSize, response.Body.Records.Count);
            page.Logs = response.Body.Records.Select(row => new AcquisitionLogRow
            {
                Id = row.Id,
                PatientId = row.PatientId ?? "",
                Status = row.Status?.ToString() ?? "",
                Phase = row.QueryPhase?.ToString() ?? "",
                Priority = row.Priority ?? "",
                Created = FacilityViewRules.When(row.CreateDate),
                Completed = FacilityViewRules.When(row.CompletionDate),
                Resources = row.ResourceTypes is not { Count: > 0 } ? "—" : string.Join(", ", row.ResourceTypes),
                Notes = row.Notes?.Count ?? 0
            }).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition log failed. ReportId={ReportId}", page.ReportId.Sanitize());
            page.SectionError = "Data acquisition service call failed: " + ex.Message;
        }

        return page;
    }

    private async Task<ValidationResultPage> LoadSummaryAsync(string? facilityId, string? reportId, CancellationToken cancellationToken)
    {
        var opened = await OpenReportAsync(facilityId, reportId, cancellationToken);
        var page = Copy<ValidationResultPage>(opened.Page);
        if (page.LoadError is not null || page.NotFound)
            return page;

        if (_validation is null)
        {
            page.LoadError = ValidationNotConfigured;
            return page;
        }

        try
        {
            var summary = await _validation.GetValidationResultSummaryAsync(
                page.FacilityId!,
                page.ReportId,
                ReportsRules.InformationSeverity,
                cancellationToken);
            if (summary.StatusCode == StatusCodes.Status404NotFound)
            {
                page.IssueCount = 0;
                page.Severity = ReportsRules.InformationSeverity;
                return page;
            }

            if (!summary.IsSuccessStatusCode)
            {
                page.SummaryError = FacilityFormRules.ServiceMessage("Validation", summary.StatusCode, summary.RawBody);
                return page;
            }

            if (!ReportsRules.TryParseResultSummary(summary.Body, out var count, out var severity))
            {
                page.SummaryError = "Validation returned a summary this page could not read.";
                return page;
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
            page.LoadError = "Validation service call failed: " + ex.Message;
        }

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

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
