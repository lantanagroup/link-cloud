using System.Globalization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reporting;

public sealed class ReportingService : IReportingService
{
    private readonly IFacilityGateway _facilityGateway;
    private readonly IReportGateway _reportGateway;
    private readonly IDataAcquisitionGateway _dataAcquisitionGateway;
    private readonly IAcknowledgementService _acknowledgementService;
    private readonly IValidationGateway _validationGateway;
    private readonly INhsnUserContext _userContext;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(
        IFacilityGateway facilityGateway,
        IReportGateway reportGateway,
        IDataAcquisitionGateway dataAcquisitionGateway,
        IAcknowledgementService acknowledgementService,
        IValidationGateway validationGateway,
        INhsnUserContext userContext,
        ILogger<ReportingService> logger)
    {
        _facilityGateway = facilityGateway;
        _reportGateway = reportGateway;
        _dataAcquisitionGateway = dataAcquisitionGateway;
        _acknowledgementService = acknowledgementService;
        _validationGateway = validationGateway;
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
    // "Discharge", not "Adhoc": an ad hoc report does trigger its own acquisition, on demand, for
    // the requested patients -- Tenant's AdHocReport endpoint produces GenerateReportRequested,
    // which Report's GenerateReportListener turns into a DataAcquisitionRequestedProducer call
    // carrying ReportableEvent="Adhoc" per patient. But DataAcquisition's
    // PatientDataService.CreateLogEntries resolves which plan to search for via
    // ReportableEventToQueryPlanTypeFactory.GenerateQueryPlanTypeFromReportableEvent, which maps
    // ReportableEvent.Adhoc -> Frequency.Discharge (not Frequency.Adhoc). So the acquisition still
    // fails to find anything unless a "Discharge"-type plan already exists for the facility --
    // Frequency.Adhoc is a plan-type option nothing ever searches for.
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

    public async Task<MeasureReportResource?> GetPatientMeasureReportResourceAsync(string reportId, string patientId, string reportType, CancellationToken cancellationToken = default)
    {
        var export = await _reportGateway.GetPatientMeasureReportExportAsync(reportId, patientId, reportType, cancellationToken);
        if (export is null)
        {
            return null;
        }

        return new MeasureReportResource
        {
            Id = export.MeasureReportId ?? $"{reportId}-{patientId}-{reportType}",
            Measure = export.ReportType,
            Date = DateTime.UtcNow.ToString("O"),
            Reporter = _userContext.FacilityName is null ? null : new MeasureReportReporter {Display = _userContext.FacilityName},
            Period = export.PeriodStart is null || export.PeriodEnd is null
                ? null
                : new MeasureReportPeriod
                {
                    Start = export.PeriodStart.Value.ToString("yyyy-MM-dd"),
                    End = export.PeriodEnd.Value.ToString("yyyy-MM-dd")
                },
            Subject = new MeasureReportReference {Reference = $"Patient/{patientId}"},
            EvaluatedResource = export.EvaluatedResources
                .Select(resource => new MeasureReportReference {Reference = $"{resource.ResourceType}/{resource.ResourceId}"})
                .ToArray(),
            Extension =
            [
                new MeasureReportExtension {Url = "urn:nhsn-link:reportingStatus", ValueString = export.ReportingStatus}
            ]
        };
    }

    public Task<bool?> GetReportAccuracyAcknowledgementAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        return _acknowledgementService.GetLatestAsync(facilityId, AcknowledgementKind.ReportAccuracy, reportId, cancellationToken);
    }

    public Task RecordReportAccuracyAcknowledgementAsync(string reportId, bool accepted, string statementKey, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        return _acknowledgementService.RecordAsync(
            facilityId, AcknowledgementKind.ReportAccuracy, reportId, accepted, statementKey, _userContext.ExternalUserId, cancellationToken);
    }

    public Task<IReadOnlyList<PreQualIssue>> GetPatientPreQualResultsAsync(string reportId, string patientId, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        return _validationGateway.GetPatientResultsAsync(facilityId, reportId, patientId, cancellationToken);
    }

    private static DateTime ParseDate(string? value) =>
        DateTime.SpecifyKind(
            DateTime.ParseExact(value!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Utc);
}
