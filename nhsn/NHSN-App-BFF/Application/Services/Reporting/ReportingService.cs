using System.Globalization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reporting;

public sealed class ReportingService : IReportingService
{
    private readonly IFacilityGateway _facilityGateway;
    private readonly IReportGateway _reportGateway;
    private readonly IDataAcquisitionGateway _dataAcquisitionGateway;
    private readonly INhsnUserContext _userContext;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(
        IFacilityGateway facilityGateway,
        IReportGateway reportGateway,
        IDataAcquisitionGateway dataAcquisitionGateway,
        INhsnUserContext userContext,
        ILogger<ReportingService> logger)
    {
        _facilityGateway = facilityGateway;
        _reportGateway = reportGateway;
        _dataAcquisitionGateway = dataAcquisitionGateway;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<ReportSummary> RequestReportAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        // Distinct because two NHSN measures can map to one dQM and Tenant refuses a request that
        // names one twice -- the UI picks measures, not dQMs, so the collapse belongs here.
        var reportTypes = request.Measures
            .Select(measure => measure.Trim())
            .Where(measure => measure.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var patientIds = request.PatientIds
            .Select(patientId => patientId.Trim())
            .Where(patientId => patientId.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        _logger.LogWarning(
            "Requesting an ad hoc report for facility {FacilityId} without ensuring a query plan. Query-plan templates are not available, so acquisition depends on a plan already configured for the facility.",
            facilityId);

        var reportId = await _facilityGateway.RequestAdHocReportAsync(
            new AdHocReportCommand
            {
                FacilityId = facilityId,
                ReportTypes = reportTypes,
                StartDate = ParseDate(request.StartDate),
                EndDate = ParseDate(request.EndDate).AddDays(1).AddSeconds(-1),
                PatientIds = patientIds
            },
            cancellationToken);

        return new ReportSummary
        {
            ReportId = reportId,
            Measures = reportTypes,
            PatientCount = patientIds.Length,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreateDate = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Status = "Pending"
        };
    }

    public Task<Paged<ReportSummary>> ListReportsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        return _reportGateway.ListReportsAsync(facilityId, page, pageSize, cancellationToken);
    }

    public Task<ReportDetail?> GetReportAsync(string reportId, CancellationToken cancellationToken = default) =>
        _reportGateway.GetReportAsync(reportId, cancellationToken);

    public Task<List<ReportPatientEntry>> GetReportPatientsAsync(string reportId, CancellationToken cancellationToken = default) =>
        _reportGateway.GetReportPatientsAsync(reportId, cancellationToken);

    public Task<PatientMappingEvidence?> GetPatientMappingEvidenceAsync(string reportId, string patientId, CancellationToken cancellationToken = default) =>
        _reportGateway.GetPatientMappingEvidenceAsync(reportId, patientId, cancellationToken);

    // DataAcquisition scopes one query plan per facility+Frequency (Discharge/Daily/Weekly/
    // Monthly/Adhoc) -- not per EHR vendor; there is no vendor lookup to make here.
    //
    // "Discharge", not "Adhoc": DataAcquisition's real acquisition trigger
    // (PatientDataService.CreateLogEntries) resolves a facility's plan via
    // ReportableEventToQueryPlanTypeFactory.GenerateQueryPlanTypeFromReportableEvent, which maps
    // ReportableEvent.Adhoc -> Frequency.Discharge (not Frequency.Adhoc). More fundamentally, the
    // only producer of the DataAcquisitionRequested message that trigger reads
    // (QueryDispatchJob) hardcodes ReportableEvent.Discharge -- nothing in this platform ever
    // requests acquisition tagged Adhoc. Frequency.Adhoc exists as a plan-type option but nothing
    // live reads a plan filed under it. Tenant's ad hoc report request (RequestAdHocReportAsync,
    // BypassSubmission = true) evaluates against whatever the facility's normal Discharge-driven
    // feed has already acquired rather than triggering its own acquisition run, so "Discharge" is
    // the plan that actually governs what an ad hoc report can see.
    private const string OperationalQueryPlanType = "Discharge";

    public Task<QueryPlan?> GetQueryPlanAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        return _dataAcquisitionGateway.GetQueryPlanAsync(facilityId, OperationalQueryPlanType, cancellationToken);
    }

    public Task<List<AcquisitionLogEntry>> GetAcquisitionLogsAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        return _dataAcquisitionGateway.GetAcquisitionLogsAsync(facilityId, reportId, cancellationToken);
    }

    public Task<AcquisitionReportSummary?> GetAcquisitionSummaryAsync(string reportId, CancellationToken cancellationToken = default) =>
        _dataAcquisitionGateway.GetReportSummaryAsync(reportId, cancellationToken);

    private static DateTime ParseDate(string? value) =>
        DateTime.SpecifyKind(
            DateTime.ParseExact(value!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Utc);
}
