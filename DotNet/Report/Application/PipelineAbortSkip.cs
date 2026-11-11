using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Report.Application;

/// <summary>
/// Shared abort check for Report Kafka listeners. Facility-scoped leftover
/// teardown and report-scoped Admin abort both land here.
/// </summary>
internal static class PipelineAbortSkip
{
    public static async Task<bool> ShouldSkipAsync(
        IServiceProvider services,
        ILogger logger,
        string listenerName,
        string? facilityId,
        string? reportId,
        CancellationToken cancellationToken)
    {
        var abortRegistry = services.GetService<IPipelineAbortRegistry>();
        if (abortRegistry is null)
            return false;

        if (!await abortRegistry.IsAbortedAsync(facilityId, reportId, cancellationToken))
            return false;

        logger.LogInformation(
            "{Listener}: Skipping aborted pipeline FacilityId={FacilityId}, ReportId={ReportId}. The event is not applied.",
            listenerName,
            (facilityId ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).SanitizeForLog(),
            (reportId ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).SanitizeForLog());
        return true;
    }
}
