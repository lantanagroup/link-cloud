using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

/// <summary>The DataAcquisition service, in our vocabulary. Backs the Report Details "View Query
/// Plan" / "View Acquisition Log" / "Export Report Summary" actions.</summary>
public interface IDataAcquisitionGateway
{
    /// <summary>
    /// Reads the facility's configured query plan. DataAcquisition scopes one query plan per
    /// facility+<c>type</c>, where <c>type</c> is a reporting Frequency (Discharge/Daily/Weekly/
    /// Monthly/Adhoc) -- not the facility's EHR vendor, despite how similar-looking code elsewhere
    /// reads. See ReportingService.GetQueryPlanAsync for why "Discharge" is the value that
    /// actually reflects what governs acquisition, not "Adhoc" as the name would suggest.
    /// </summary>
    Task<QueryPlan?> GetQueryPlanAsync(string facilityId, string type, CancellationToken cancellationToken = default);

    /// <summary>Reads the acquisition log entries recorded for this report.</summary>
    Task<List<AcquisitionLogEntry>> GetAcquisitionLogsAsync(string facilityId, string reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads DataAcquisition's own summary counts for this report.</summary>
    Task<AcquisitionReportSummary?> GetReportSummaryAsync(string reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a facility's QueryPlan. Returns <c>false</c> (not an error) when a plan already
    /// exists for that facility+Type -- DataAcquisition has no upsert on create, callers must PUT
    /// instead.
    /// </summary>
    Task<bool> CreateQueryPlanAsync(string facilityId, CreateQueryPlanRequestApiModel request, CancellationToken cancellationToken = default);

    /// <summary>Replaces an existing facility's QueryPlan for the request's Type.</summary>
    Task UpdateQueryPlanAsync(string facilityId, object request, CancellationToken cancellationToken = default);
}
