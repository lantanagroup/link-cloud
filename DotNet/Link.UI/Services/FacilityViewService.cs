using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// Facility view: enrolled reporting, the facility's reports, HSLOC locations, organization
/// locations, encounters, and DMRP plans. Reads go through LinkSDK. Abort, cleanup, and restore
/// go through Admin.BFF because those calls also change acquisition logs.
/// </summary>
public sealed class FacilityViewService
{
    public const string ReportNotConfigured =
        "Report service URL is not configured (ServiceRegistry:ReportServiceUrl).";
    public const string AcquisitionNotConfigured =
        "Data acquisition service URL is not configured (ServiceRegistry:DataAcquisitionServiceUrl).";
    public const string NormalizationNotConfigured =
        "Normalization service URL is not configured (ServiceRegistry:NormalizationServiceUrl).";
    public const string AdminNotConfigured =
        "Admin.BFF service URL is not configured (ServiceRegistry:AdminBffServiceUrl).";

    private readonly IFacilityServiceClient _facilities;
    private readonly IReportServiceClient? _reports;
    private readonly IDataAcquisitionServiceClient? _acquisition;
    private readonly INormalizationServiceClient? _normalization;
    private readonly IDmrpServiceClient? _dmrp;
    private readonly IAdminBffIntegrationClient? _admin;
    private readonly LinkUiFeatureOptions _options;
    private readonly ILogger<FacilityViewService> _logger;

    public FacilityViewService(
        IFacilityServiceClient facilities,
        IReportServiceClient? reports,
        IDataAcquisitionServiceClient? acquisition,
        INormalizationServiceClient? normalization,
        IDmrpServiceClient? dmrp,
        IAdminBffIntegrationClient? admin,
        IOptions<LinkUiFeatureOptions> options,
        ILogger<FacilityViewService> logger)
    {
        _facilities = facilities;
        _reports = reports;
        _acquisition = acquisition;
        _normalization = normalization;
        _dmrp = dmrp;
        _admin = admin;
        _options = options.Value;
        _logger = logger;
    }

    public static FacilityViewService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        var options = services.GetRequiredService<IOptions<LinkUiFeatureOptions>>();
        return new FacilityViewService(
            services.GetRequiredService<IFacilityServiceClient>(),
            Blank(registry.ReportServiceUrl) ? null : services.GetRequiredService<IReportServiceClient>(),
            Blank(registry.DataAcquisitionServiceUrl) ? null : services.GetRequiredService<IDataAcquisitionServiceClient>(),
            Blank(registry.NormalizationServiceUrl) ? null : services.GetRequiredService<INormalizationServiceClient>(),
            options.Value.DmrpEnabled ? services.GetRequiredService<IDmrpServiceClient>() : null,
            Blank(registry.AdminBffServiceUrl) ? null : services.GetRequiredService<IAdminBffIntegrationClient>(),
            options,
            services.GetRequiredService<ILogger<FacilityViewService>>());
    }

    public async Task<FacilityViewModel> LoadAsync(string? facilityId, FacilityViewQuery? query, CancellationToken cancellationToken)
    {
        query ??= new FacilityViewQuery();
        var page = new FacilityViewModel
        {
            FacilityId = facilityId?.Trim(),
            Query = query,
            Section = FacilityViewRules.NormalizeSection(query.Section),
            DmrpEnabled = _options.DmrpEnabled
        };
        query.Section = page.Section;

        if (!await LoadFacilityAsync(page, cancellationToken))
            return page;

        try
        {
            switch (page.Section)
            {
                case "hsloc":
                    await LoadHslocAsync(page, cancellationToken);
                    break;
                case "locations":
                    await LoadLocationsAsync(page, cancellationToken);
                    break;
                case "encounters":
                    await LoadEncountersAsync(page, cancellationToken);
                    break;
                case "plans":
                    await LoadPlansAsync(page, cancellationToken);
                    break;
                default:
                    await LoadReportsAsync(page, cancellationToken);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facility view section failed. FacilityId={FacilityId} Section={Section}", page.FacilityId.Sanitize(), page.Section);
            page.SectionError = "That section failed: " + ex.Message;
        }

        return page;
    }

    public async Task<ReportDetailModel> LoadReportAsync(
        string? facilityId,
        string? reportId,
        ReportPageQuery? query,
        CancellationToken cancellationToken)
    {
        query ??= new ReportPageQuery();
        var page = new ReportDetailModel
        {
            FacilityId = facilityId?.Trim(),
            ReportId = reportId?.Trim() ?? string.Empty,
            Query = query
        };

        if (!FacilityFormRules.IsValidFacilityId(page.FacilityId, _options.NumericOnlyFacilityId))
        {
            page.NotFound = true;
            page.LoadError = FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId);
            return page;
        }

        var parsed = FacilityViewRules.ParseReportId(page.ReportId, out var idError);
        if (parsed is null)
        {
            page.NotFound = true;
            page.LoadError = idError ?? "Report ID is required.";
            return page;
        }

        page.ReportId = parsed.Value.ToString();
        if (_reports is null)
        {
            page.LoadError = ReportNotConfigured;
            return page;
        }

        try
        {
            var facility = await _facilities.GetAsync(page.FacilityId!, cancellationToken);
            if (facility.StatusCode == StatusCodes.Status404NotFound)
            {
                page.NotFound = true;
                return page;
            }

            if (!facility.IsSuccessStatusCode || facility.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Tenant", facility.StatusCode, facility.RawBody);
                return page;
            }

            page.FacilityName = facility.Body.FacilityName;
            var schedule = await _reports.GetScheduleAsync(page.ReportId, cancellationToken);
            if (schedule.StatusCode == StatusCodes.Status404NotFound || schedule.Body is null)
            {
                page.NotFound = true;
                page.LoadError = "That report was not found.";
                return page;
            }

            if (!schedule.IsSuccessStatusCode)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Report", schedule.StatusCode, schedule.RawBody);
                return page;
            }

            if (!string.Equals(schedule.Body.FacilityId, page.FacilityId, StringComparison.Ordinal))
            {
                page.NotFound = true;
                page.LoadError = "That report is not for this facility.";
                return page;
            }

            var counts = await CountsAsync(schedule.Body.Id, cancellationToken);
            page.Report = ToReportRow(schedule.Body, counts.Census, counts.Population);

            var pageSize = FacilityViewRules.ClampPageSize(query.PageSize);
            var pageNumber = FacilityViewRules.ClampPage(query.Page);
            var patients = await _reports.SearchEntriesAsync(
                page.FacilityId,
                FacilityViewRules.Clean(query.PatientId),
                page.ReportId,
                pageSize: pageSize,
                pageNumber: pageNumber,
                cancellationToken: cancellationToken);

            if (!patients.IsSuccessStatusCode || patients.Body is null)
            {
                page.SectionError = FacilityFormRules.ServiceMessage("Report", patients.StatusCode, patients.RawBody);
                return page;
            }

            page.Paging = Bar(patients.Body.Metadata, pageNumber, pageSize, patients.Body.Records.Count);
            page.Patients = patients.Body.Records.Select(ToPatient).ToList();

            var selected = FacilityViewRules.Clean(query.Patient);
            if (selected is not null)
                page.Patient = await LoadPatientAsync(page.ReportId, selected, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report view failed. FacilityId={FacilityId}", page.FacilityId.Sanitize());
            page.LoadError = "Report service call failed: " + ex.Message;
        }

        return page;
    }

    public Task<FacilityViewAction> ResubmitAsync(string? facilityId, string? reportId, bool bypassSubmission, CancellationToken cancellationToken) =>
        ChangeReportAsync(facilityId, reportId, includeDeleted: false, cancellationToken, async (id, report, schedule) =>
        {
            if (!FacilityViewRules.CanResubmit(schedule.Status, schedule.IsDeleted == true))
                return Fail("Only a submitted report can be resubmitted.");

            var response = await _facilities.RegenerateReportAsync(id, new RegenerateReportRequest
            {
                ReportId = report,
                BypassSubmission = bypassSubmission
            }, cancellationToken);

            return response.IsSuccessStatusCode
                ? Done("Report resubmitted.")
                : Fail(FacilityFormRules.ServiceMessage("Tenant", response.StatusCode, response.RawBody));
        });

    public Task<FacilityViewAction> AbortAsync(string? facilityId, string? reportId, CancellationToken cancellationToken) =>
        ChangeReportAsync(facilityId, reportId, includeDeleted: false, cancellationToken, async (_, report, schedule) =>
        {
            if (!FacilityViewRules.CanAbort(schedule.Status, schedule.IsDeleted == true))
                return Fail("Only an in-progress report can be aborted.");
            if (_admin is null)
                return Fail(AdminNotConfigured);

            var response = await _admin.AbortAggregateReportAsync(report, cancellationToken);
            return response.IsSuccessStatusCode
                ? Done("Report aborted. In-flight work for this report will stop.")
                : Fail(FacilityFormRules.ServiceMessage("Admin", response.StatusCode, response.RawBody));
        });

    public Task<FacilityViewAction> CleanUpAsync(string? facilityId, string? reportId, CancellationToken cancellationToken) =>
        ChangeReportAsync(facilityId, reportId, includeDeleted: false, cancellationToken, async (_, report, schedule) =>
        {
            if (!FacilityViewRules.CanCleanUp(schedule.Status, schedule.IsDeleted == true))
                return Fail("This report cannot be cleaned up.");
            if (_admin is null)
                return Fail(AdminNotConfigured);

            var response = await _admin.DeleteAggregateReportAsync(report, cancellationToken);
            return response.IsSuccessStatusCode
                ? Done("Report cleaned up. It can be restored later.")
                : Fail(FacilityFormRules.ServiceMessage("Admin", response.StatusCode, response.RawBody));
        });

    public Task<FacilityViewAction> RestoreReportAsync(string? facilityId, string? reportId, CancellationToken cancellationToken) =>
        ChangeReportAsync(facilityId, reportId, includeDeleted: true, cancellationToken, async (_, report, schedule) =>
        {
            if (schedule.IsDeleted != true)
                return Fail("That report is not deleted.");
            if (_admin is null)
                return Fail(AdminNotConfigured);

            var response = await _admin.RestoreAggregateReportAsync(report, cancellationToken);
            return response.IsSuccessStatusCode
                ? Done("Report restored.")
                : Fail(FacilityFormRules.ServiceMessage("Admin", response.StatusCode, response.RawBody));
        });

    public async Task<FacilityViewAction> RestoreFacilityAsync(string? facilityId, CancellationToken cancellationToken)
    {
        var id = facilityId?.Trim() ?? string.Empty;
        if (!FacilityFormRules.IsValidFacilityId(id, _options.NumericOnlyFacilityId))
            return Fail(FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId));
        if (_admin is null)
            return Fail(AdminNotConfigured);

        var response = await _admin.RestoreAggregateFacilityAsync(id, cancellationToken);
        return response.IsSuccessStatusCode
            ? Done("Facility restored.")
            : Fail(FacilityFormRules.ServiceMessage("Admin", response.StatusCode, response.RawBody));
    }

    private async Task<bool> LoadFacilityAsync(FacilityViewModel page, CancellationToken cancellationToken)
    {
        if (!FacilityFormRules.IsValidFacilityId(page.FacilityId, _options.NumericOnlyFacilityId))
        {
            page.NotFound = true;
            page.LoadError = FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId);
            return false;
        }

        var facility = await _facilities.GetAsync(page.FacilityId!, cancellationToken);
        if (facility.StatusCode == StatusCodes.Status404NotFound)
        {
            page.NotFound = true;
            return false;
        }

        if (!facility.IsSuccessStatusCode || facility.Body is null)
        {
            page.LoadError = FacilityFormRules.ServiceMessage("Tenant", facility.StatusCode, facility.RawBody);
            return false;
        }

        page.FacilityName = facility.Body.FacilityName;
        page.Enrolled = FacilityViewRules.Enrolled(facility.Body.ScheduledReports);
        return true;
    }

    private async Task LoadReportsAsync(FacilityViewModel page, CancellationToken cancellationToken)
    {
        if (_reports is null)
        {
            page.SectionError = ReportNotConfigured;
            return;
        }

        var reportId = FacilityViewRules.ParseReportId(page.Query.ReportId, out var idError);
        if (idError is not null)
        {
            page.SectionError = idError;
            return;
        }

        var pageSize = FacilityViewRules.ClampPageSize(page.Query.PageSize);
        var pageNumber = FacilityViewRules.ClampPage(page.Query.Page);
        var response = await _reports.SearchFacilitySchedulesAsync(new ReportScheduleSearch
        {
            FacilityId = page.FacilityId,
            Frequency = FacilityViewRules.ParseFrequency(page.Query.Frequency),
            ReportStartDate = FacilityViewRules.PeriodStart(FacilityViewRules.ParseDate(page.Query.PeriodFrom)),
            ReportEndDate = FacilityViewRules.PeriodEnd(FacilityViewRules.ParseDate(page.Query.PeriodTo)),
            Statuses = FacilityViewRules.ParseStatuses(page.Query.Status),
            IncludeDeleted = page.Query.ShowDeleted,
            SortBy = FacilityViewRules.AllowedSort(page.Query.SortBy, FacilityViewRules.ReportSorts),
            SortOrder = FacilityViewRules.ParseSortDirection(page.Query.SortDir),
            PageSize = pageSize,
            PageNumber = pageNumber,
            CreateDate = FacilityViewRules.ParseDate(page.Query.Created),
            Id = reportId
        }, cancellationToken);

        if (response.StatusCode == StatusCodes.Status204NoContent)
        {
            page.Paging = new PageBar { Page = 1, PageSize = pageSize };
            return;
        }

        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            page.SectionError = FacilityFormRules.ServiceMessage("Report", response.StatusCode, response.RawBody);
            return;
        }

        var records = response.Body.Records;
        var counts = await Task.WhenAll(records.Select(record => CountsAsync(record.Id, cancellationToken)));
        page.Paging = Bar(response.Body.Metadata, pageNumber, pageSize, records.Count);
        page.Reports = records.Select((record, index) => ToReportRow(record, counts[index].Census, counts[index].Population)).ToList();
    }

    private async Task<(int? Census, int? Population)> CountsAsync(Guid reportId, CancellationToken cancellationToken)
    {
        if (_reports is null)
            return (null, null);

        var id = reportId.ToString();
        var census = _reports.GetEntryCountByScheduleAsync(id, cancellationToken);
        var population = _reports.GetInitialPopulationCountAsync(id, cancellationToken);
        await Task.WhenAll(census, population);
        return (Count(await census), Count(await population));
    }

    private static int? Count(LinkApiResponse<int> response) =>
        response.IsSuccessStatusCode ? response.Body : null;

    private async Task LoadLocationsAsync(FacilityViewModel page, CancellationToken cancellationToken)
    {
        if (_acquisition is null)
        {
            page.SectionError = AcquisitionNotConfigured;
            return;
        }

        var pageSize = FacilityViewRules.ClampPageSize(page.Query.PageSize);
        var pageNumber = FacilityViewRules.ClampPage(page.Query.Page);
        var response = await _acquisition.SearchOrganizationLocationMappingsAsync(
            page.FacilityId!,
            FacilityViewRules.Clean(page.Query.LocationId),
            FacilityViewRules.Clean(page.Query.LocationName),
            FacilityViewRules.Clean(page.Query.LocationAlias),
            FacilityViewRules.Clean(page.Query.PartOf),
            FacilityViewRules.ParseTriState(page.Query.OrgLocation),
            page.Query.ShowInactive ? null : true,
            FacilityViewRules.AllowedSort(page.Query.SortBy, FacilityViewRules.LocationSorts),
            FacilityViewRules.ParseSortDirection(page.Query.SortDir),
            pageSize,
            pageNumber,
            cancellationToken);

        if (!ApplyPage(page, response, pageNumber, pageSize, "Data acquisition", out var records))
            return;

        page.Locations = records.Select(ToLocation).ToList();
    }

    private async Task LoadEncountersAsync(FacilityViewModel page, CancellationToken cancellationToken)
    {
        if (_acquisition is null)
        {
            page.SectionError = AcquisitionNotConfigured;
            return;
        }

        var pageSize = FacilityViewRules.ClampPageSize(page.Query.PageSize);
        var pageNumber = FacilityViewRules.ClampPage(page.Query.Page);
        var response = await _acquisition.SearchEncounterMappingsAsync(
            page.FacilityId!,
            FacilityViewRules.Clean(page.Query.EncounterId),
            FacilityViewRules.Clean(page.Query.PatientId),
            FacilityViewRules.ParseTriState(page.Query.Mapped),
            FacilityViewRules.AllowedSort(page.Query.SortBy, FacilityViewRules.EncounterSorts),
            FacilityViewRules.ParseSortDirection(page.Query.SortDir),
            pageSize,
            pageNumber,
            cancellationToken);

        if (!ApplyPage(page, response, pageNumber, pageSize, "Data acquisition", out var records))
            return;

        page.Encounters = records.Select(ToEncounter).ToList();
        if (page.Query.Location is > 0)
            await LoadLocationDetailAsync(page, page.Query.Location.Value, cancellationToken);
    }

    private async Task LoadLocationDetailAsync(FacilityViewModel page, int id, CancellationToken cancellationToken)
    {
        var response = await _acquisition!.GetOrganizationLocationMappingAsync(id, cancellationToken);
        if (response.StatusCode == StatusCodes.Status404NotFound || response.Body is null)
        {
            page.LocationDetailError = "Location not found.";
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            page.LocationDetailError = FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
            return;
        }

        if (!string.Equals(response.Body.FacilityId, page.FacilityId, StringComparison.Ordinal))
        {
            page.LocationDetailError = "That location is not for this facility.";
            return;
        }

        page.LocationDetail = ToLocation(response.Body);
    }

    private async Task LoadHslocAsync(FacilityViewModel page, CancellationToken cancellationToken)
    {
        if (_normalization is null)
        {
            page.SectionError = NormalizationNotConfigured;
            return;
        }

        var response = await _normalization.GetFacilityLocationsAsync(page.FacilityId!, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            page.SectionError = FacilityFormRules.ServiceMessage("Normalization", response.StatusCode, response.RawBody);
            return;
        }

        page.LocationTree = FacilityViewRules.BuildLocationTree(response.Body.Records);
        page.SelectedLocation = FacilityViewRules.FindLocation(page.LocationTree, page.Query.Hsloc);
    }

    private async Task LoadPlansAsync(FacilityViewModel page, CancellationToken cancellationToken)
    {
        if (!_options.DmrpEnabled)
        {
            page.SectionError = "DMRP is off, so this facility has no reporting plans here.";
            return;
        }

        if (_dmrp is null)
        {
            page.SectionError = "DMRP is not available.";
            return;
        }

        var response = await _dmrp.GetFacilityReportingPlansForFacilityAsync(page.FacilityId!, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            page.SectionError = FacilityFormRules.ServiceMessage("DMRP", response.StatusCode, response.RawBody);
            return;
        }

        page.Periods = FacilityViewRules.PeriodOptions(response.Body);
        page.Cadences = FacilityViewRules.CadenceOptions(response.Body);
        page.Plans = FacilityViewRules.VisiblePlans(response.Body, page.Query, out var paging);
        page.Paging = paging;
    }

    private async Task<ReportPatientDetail> LoadPatientAsync(string reportId, string patientId, CancellationToken cancellationToken)
    {
        var response = await _reports!.GetEntryByScheduleAndPatientAsync(reportId, patientId, cancellationToken);
        if (response.StatusCode == StatusCodes.Status404NotFound || response.Body is null)
            return new ReportPatientDetail { PatientId = patientId, Error = "That patient is not on this report." };

        if (!response.IsSuccessStatusCode)
        {
            return new ReportPatientDetail
            {
                PatientId = patientId,
                Error = FacilityFormRules.ServiceMessage("Report", response.StatusCode, response.RawBody)
            };
        }

        var entry = response.Body;
        var acquisition = entry.Acquisition?.LocationOrg;
        var maps = entry.Normalization?.CodeMaps ?? [];
        return new ReportPatientDetail
        {
            PatientId = entry.PatientId,
            AcquisitionMissing = entry.Acquisition is null,
            EncounterCount = acquisition?.EncounterCount ?? 0,
            OrgEncounterCount = acquisition?.OrgEncounterCount ?? 0,
            AssumedOrgEncounterCount = acquisition?.AssumedOrgEncounterCount ?? 0,
            Locations = acquisition?.Matches
                .Select(match => string.IsNullOrWhiteSpace(match.LocationName) ? match.LocationId : $"{match.LocationName} ({match.LocationId})")
                .ToList() ?? [],
            NormalizationMissing = entry.Normalization is null,
            CodeMaps = maps.Select(map =>
            {
                var unmapped = map.UnmappedCodes.Count == 0 ? "" : " Unmapped codes: " + string.Join(", ", map.UnmappedCodes) + ".";
                return $"{map.SourceSystem} to {map.TargetSystem}: {map.MappedCount} mapped, {map.UnmappedCount} unmapped, {map.FailureCount} failed.{unmapped}";
            }).ToList()
        };
    }

    private async Task<FacilityViewAction> ChangeReportAsync(
        string? facilityId,
        string? reportId,
        bool includeDeleted,
        CancellationToken cancellationToken,
        Func<string, string, ReportScheduleApiModel, Task<FacilityViewAction>> change)
    {
        var id = facilityId?.Trim();
        if (!FacilityFormRules.IsValidFacilityId(id, _options.NumericOnlyFacilityId))
            return Fail(FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId));

        var parsed = FacilityViewRules.ParseReportId(reportId, out var idError);
        if (parsed is null)
            return Fail(idError ?? "Report ID is required.");

        if (_reports is null)
            return Fail(ReportNotConfigured);

        try
        {
            var schedule = await _reports.GetScheduleAsync(parsed.Value.ToString(), cancellationToken, includeDeleted);
            if (schedule.StatusCode == StatusCodes.Status404NotFound || schedule.Body is null)
                return Fail("That report was not found.");

            if (!schedule.IsSuccessStatusCode)
                return Fail(FacilityFormRules.ServiceMessage("Report", schedule.StatusCode, schedule.RawBody));

            if (!string.Equals(schedule.Body.FacilityId, id, StringComparison.Ordinal))
                return Fail("That report is not for this facility.");

            return await change(id!, parsed.Value.ToString(), schedule.Body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report action failed. FacilityId={FacilityId}", id.Sanitize());
            return Fail("Report service call failed: " + ex.Message);
        }
    }

    private static bool ApplyPage<T>(
        FacilityViewModel page,
        LinkApiResponse<PagedConfigModel<T>> response,
        int pageNumber,
        int pageSize,
        string service,
        out List<T> records)
        where T : class
    {
        if (response.StatusCode == StatusCodes.Status204NoContent)
        {
            page.Paging = new PageBar { Page = 1, PageSize = pageSize };
            records = [];
            return false;
        }

        if (!response.IsSuccessStatusCode || response.Body is null)
        {
            page.SectionError = FacilityFormRules.ServiceMessage(service, response.StatusCode, response.RawBody);
            records = [];
            return false;
        }

        records = response.Body.Records;
        page.Paging = Bar(response.Body.Metadata, pageNumber, pageSize, records.Count);
        return true;
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

    internal static FacilityReportRow ToReportRow(ReportScheduleApiModel schedule, int? census, int? population)
    {
        var deleted = schedule.IsDeleted == true;
        return new FacilityReportRow
        {
            Id = schedule.Id,
            FacilityId = schedule.FacilityId,
            Frequency = schedule.Frequency.ToString(),
            Measures = string.Join(", ", schedule.ReportTypes),
            PeriodStart = FacilityViewRules.When(schedule.ReportStartDate),
            PeriodEnd = FacilityViewRules.When(schedule.ReportEndDate),
            Created = FacilityViewRules.When(schedule.CreateDate),
            Submitted = string.IsNullOrEmpty(FacilityViewRules.When(schedule.SubmitReportDateTime))
                ? null
                : FacilityViewRules.When(schedule.SubmitReportDateTime),
            Status = schedule.Status,
            StatusLabel = FacilityViewRules.StatusLabel(schedule.Status),
            Deleted = deleted,
            CensusCount = census,
            InitialPopulationCount = population,
            CanResubmit = FacilityViewRules.CanResubmit(schedule.Status, deleted),
            CanAbort = FacilityViewRules.CanAbort(schedule.Status, deleted),
            CanCleanUp = FacilityViewRules.CanCleanUp(schedule.Status, deleted),
            CanRestore = FacilityViewRules.CanRestore(deleted),
            CanDownload = ReportsRules.CanDownload(deleted, schedule.PayloadRootUri)
        };
    }

    private static LocationRow ToLocation(OrganizationLocationMappingApiModel row) => new()
    {
        Id = row.LocationMappingId,
        LocationId = row.LocationId,
        Name = row.LocationName,
        Alias = row.LocationAlias,
        PartOf = row.PartOfValue,
        PartOfId = row.PartOfId,
        IsOrg = row.IsOrgLocation,
        IsActive = row.IsActive,
        Created = FacilityViewRules.When(row.CreateDate),
        Modified = FacilityViewRules.When(row.ModifiedDate)
    };

    private static EncounterRow ToEncounter(EncounterMappingApiModel row) => new()
    {
        Id = row.EncounterMappingId,
        EncounterId = row.EncounterId,
        PatientId = row.PatientId,
        MappedToOrg = row.MappedToOrg,
        Created = FacilityViewRules.When(row.CreateDate),
        Modified = FacilityViewRules.When(row.ModifiedDate),
        Locations = row.EncounterLocations.Select(location => new EncounterLocationLink
        {
            LocationId = location.LocationId,
            MappingId = location.OrganizationLocationMappingId
        }).ToList()
    };

    private static ReportPatientRow ToPatient(ReportEntryApiModel row) => new()
    {
        PatientId = row.PatientId,
        ReportingStatus = row.ReportingStatus.ToString(),
        SubmissionStatus = row.SubmissionStatus?.ToString() ?? "",
        LocationOrg = row.LocationOrgStatus.ToString(),
        EncounterMapping = row.EncounterMappingStatus.ToString(),
        Hsloc = row.HslocMappingStatus.ToString()
    };

    private static FacilityViewAction Done(string message) => new(true, message);
    private static FacilityViewAction Fail(string message) => new(false, message);
    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
