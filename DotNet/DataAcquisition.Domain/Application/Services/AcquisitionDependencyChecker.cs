using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.Extensions.Logging;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IAcquisitionDependencyChecker
{
    Task<DependencyCheckResult> CheckDependenciesAsync(DataAcquisitionLogModel log, CancellationToken cancellationToken = default);
}

public record DependencyCheckResult(bool AreDependenciesMet, IReadOnlyList<string> BlockingResourceTypes)
{
    public static DependencyCheckResult Met { get; } = new(true, Array.Empty<string>());
}

public class AcquisitionDependencyChecker : IAcquisitionDependencyChecker
{
    private readonly IQueryPlanQueries _queryPlanQueries;
    private readonly IDataAcquisitionLogQueries _logQueries;
    private readonly ILogger<AcquisitionDependencyChecker> _logger;

    public AcquisitionDependencyChecker(
        IQueryPlanQueries queryPlanQueries,
        IDataAcquisitionLogQueries logQueries,
        ILogger<AcquisitionDependencyChecker> logger)
    {
        _queryPlanQueries = queryPlanQueries ?? throw new ArgumentNullException(nameof(queryPlanQueries));
        _logQueries = logQueries ?? throw new ArgumentNullException(nameof(logQueries));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DependencyCheckResult> CheckDependenciesAsync(DataAcquisitionLogModel log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        using var activity = ServiceActivitySource.Instance.StartActivity("AcquisitionDependencyChecker.CheckDependenciesAsync");
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, log.Id);
        activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId);

        if (string.IsNullOrWhiteSpace(log.CorrelationId))
            return DependencyCheckResult.Met;

        // Only Initial/Supplemental phases have ResourceIds dependencies worth checking.
        if (log.QueryPhase is not (QueryPhase.Initial or QueryPhase.Supplemental))
            return DependencyCheckResult.Met;

        // Resolve frequency the same way PatientDataService.CreateLogEntries does, so the
        // dep check looks up the same query plan that was used to build this log's FhirQueries.
        // Prefer ReportableEvent → Frequency via the factory (e.g., Adhoc maps to Discharge);
        // fall back to ScheduledReport.Frequency if no ReportableEvent is set.
        Frequency? frequency = null;
        if (log.ReportableEvent.HasValue)
        {
            try
            {
                frequency = ReportableEventToQueryPlanTypeFactory.GenerateQueryPlanTypeFromReportableEvent(log.ReportableEvent.Value);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Could not resolve Frequency from ReportableEvent {Event} for LogId {LogId}; skipping dependency check.",
                    log.ReportableEvent, log.Id);
                return DependencyCheckResult.Met;
            }
        }
        else if (log.ScheduledReport != null)
        {
            frequency = log.ScheduledReport.Frequency;
        }

        if (!frequency.HasValue)
        {
            _logger.LogWarning("No ReportableEvent or ScheduledReport frequency available for LogId {LogId}; skipping dependency check.", log.Id);
            return DependencyCheckResult.Met;
        }

        var queryPlan = await _queryPlanQueries.GetAsync(log.FacilityId, frequency.Value, cancellationToken);
        if (queryPlan is null)
        {
            _logger.LogWarning("No query plan found for FacilityId {FacilityId} Frequency {Frequency}; skipping dependency check for LogId {LogId}.",
                log.FacilityId, frequency, log.Id);
            return DependencyCheckResult.Met;
        }

        var phaseQueries = log.QueryPhase == QueryPhase.Initial
            ? queryPlan.InitialQueries
            : queryPlan.SupplementalQueries;

        if (phaseQueries is null)
            return DependencyCheckResult.Met;

        // Resource types this log covers (from its FhirQuery entries).
        var logResourceTypes = log.FhirQuery?
            .SelectMany(q => q.ResourceTypes ?? [])
            .Select(r => r.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (logResourceTypes.Count == 0)
            return DependencyCheckResult.Met;

        // Find ResourceIdsParameter dependencies from matching ParameterQueryConfig entries.
        var dependencyResourceTypes = phaseQueries
            .Select(kvp => kvp.Value)
            .OfType<ParameterQueryConfig>()
            .Where(cfg => !string.IsNullOrWhiteSpace(cfg.ResourceType) && logResourceTypes.Contains(cfg.ResourceType))
            .SelectMany(cfg => cfg.Parameters ?? Enumerable.Empty<IParameter>())
            .OfType<ResourceIdsParameter>()
            .Select(p => p.Resource)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dependencyResourceTypes.Count == 0)
            return DependencyCheckResult.Met;

        var blocking = await _logQueries.GetNonTerminalDependencyResourceTypes(
            log.CorrelationId, log.FacilityId, dependencyResourceTypes, cancellationToken);

        if (blocking.Count == 0)
            return DependencyCheckResult.Met;

        return new DependencyCheckResult(false, blocking);
    }

}
