using System.Globalization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reporting;

public sealed class ReportingService : IReportingService
{
    private readonly IFacilityGateway _facilityGateway;
    private readonly INhsnUserContext _userContext;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(
        IFacilityGateway facilityGateway,
        INhsnUserContext userContext,
        ILogger<ReportingService> logger)
    {
        _facilityGateway = facilityGateway;
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

    private static DateTime ParseDate(string? value) =>
        DateTime.SpecifyKind(
            DateTime.ParseExact(value!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Utc);
}
