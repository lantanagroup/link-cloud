using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IReferentialPhasePromoter
{
    /// <summary>
    /// Drain the <c>PendingReferenceIds</c> for one correlation into newly-created
    /// referential <see cref="Infrastructure.Entities.DataAcquisitionLog"/> rows
    /// (one per resource type). Idempotent and transactional: if a referential log
    /// already exists for this correlation the pending rows are cleaned up and the
    /// call is a no-op.
    /// </summary>
    Task<int> PromoteAsync(string facilityId, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find every <c>(FacilityId, CorrelationId)</c> pair with staged references whose
    /// <see cref="QueryPhase.Initial"/> phase is terminal and no referential phase
    /// log exists yet, and promote each. Safe to call on every scheduler tick — the
    /// per-correlation logic is idempotent.
    /// </summary>
    Task<int> FindAndPromoteReadyCorrelationsAsync(int maxCorrelationsPerRun, CancellationToken cancellationToken = default);
}

public class ReferentialPhasePromoter : IReferentialPhasePromoter
{
    private static readonly RequestStatus[] TerminalStatuses =
    {
        RequestStatus.Completed,
        RequestStatus.MaxRetriesReached,
        RequestStatus.Skipped,
        RequestStatus.Cancelled,
        RequestStatus.ConfigurationMissing,
    };

    private readonly ILogger<ReferentialPhasePromoter> _logger;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly IQueryPlanQueries _queryPlanQueries;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;

    public ReferentialPhasePromoter(
        ILogger<ReferentialPhasePromoter> logger,
        DataAcquisitionDbContext dbContext,
        IQueryPlanQueries queryPlanQueries,
        IDataAcquisitionLogManager dataAcquisitionLogManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _queryPlanQueries = queryPlanQueries ?? throw new ArgumentNullException(nameof(queryPlanQueries));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
    }

    public async Task<int> FindAndPromoteReadyCorrelationsAsync(int maxCorrelationsPerRun, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("ReferentialPhasePromoter.FindAndPromoteReadyCorrelationsAsync");

        if (maxCorrelationsPerRun <= 0) maxCorrelationsPerRun = 50;

        // Distinct (facility, correlation) candidates with staged references.
        var candidates = await _dbContext.PendingReferenceIds.AsNoTracking()
            .GroupBy(p => new { p.FacilityId, p.CorrelationId })
            .Select(g => new { g.Key.FacilityId, g.Key.CorrelationId })
            .Take(maxCorrelationsPerRun)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return 0;

        int promoted = 0;
        foreach (var c in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await IsInitialPhaseTerminalAsync(c.FacilityId, c.CorrelationId, cancellationToken))
                continue;

            // Already promoted? (idempotency — cheap pre-check; authoritative check
            // is re-done inside PromoteAsync under the transaction.)
            if (await ReferentialLogExistsAsync(c.FacilityId, c.CorrelationId, cancellationToken))
            {
                await PurgePendingAsync(c.FacilityId, c.CorrelationId, cancellationToken);
                continue;
            }

            try
            {
                var created = await PromoteAsync(c.FacilityId, c.CorrelationId, cancellationToken);
                if (created > 0)
                    promoted++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ReferentialPhasePromoter.FindAndPromoteReadyCorrelationsAsync: promotion failed for facility {FacilityId} correlation {CorrelationId}; will retry on next run.",
                    c.FacilityId, c.CorrelationId);
            }
        }

        return promoted;
    }

    public async Task<int> PromoteAsync(string facilityId, string correlationId, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("ReferentialPhasePromoter.PromoteAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, correlationId);

        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("FacilityId is required.", nameof(facilityId));
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        // Snapshot pending rows before opening the transaction so we fail-fast on
        // empty work lists without holding a transaction open.
        var pending = await _dbContext.PendingReferenceIds.AsNoTracking()
            .Where(p => p.FacilityId == facilityId && p.CorrelationId == correlationId)
            .Select(p => new { p.ResourceType, p.ResourceId })
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return 0;

        // Find a representative primary-phase log so we can copy correlation-scoped
        // metadata (ReportTrackingId, ReportableEvent, TraceId, ScheduledReport/Frequency).
        var seedLog = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .Where(l => l.FacilityId == facilityId
                     && l.CorrelationId == correlationId
                     && l.QueryPhase == QueryPhase.Initial
                     && !l.IsDeleted)
            .OrderBy(l => l.Id)
            .Select(l => new
            {
                l.ReportTrackingId,
                l.ReportableEvent,
                l.TraceId,
                l.Priority,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (seedLog == null)
        {
            _logger.LogWarning(
                "ReferentialPhasePromoter.PromoteAsync: no Initial-phase log found for facility {FacilityId} correlation {CorrelationId}; dropping {Count} orphaned pending reference id(s).",
                facilityId, correlationId, pending.Count);
            await PurgePendingAsync(facilityId, correlationId, cancellationToken);
            return 0;
        }

        if (seedLog.ReportableEvent == null)
        {
            _logger.LogWarning(
                "ReferentialPhasePromoter.PromoteAsync: Initial-phase log for facility {FacilityId} correlation {CorrelationId} has no ReportableEvent; cannot resolve a query plan. Leaving {Count} pending reference id(s) in place for retry.",
                facilityId, correlationId, pending.Count);
            return 0;
        }

        // Resolve the query-plan Frequency the same way the primary acquisition path does
        // (PatientDataService): from the log's ReportableEvent. The ScheduledReport.Frequency
        // (e.g. Adhoc) is not necessarily the QueryPlan.Type that was actually used to
        // execute the primary acquisition — for example ReportableEvent.Adhoc maps to
        // Frequency.Discharge for plan lookup. Using the ScheduledReport.Frequency directly
        // would miss the matching plan and cause the staged ids to be dropped.
        Frequency planFrequency;
        try
        {
            planFrequency = ReportableEventToQueryPlanTypeFactory
                .GenerateQueryPlanTypeFromReportableEvent(seedLog.ReportableEvent.Value);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex,
                "ReferentialPhasePromoter.PromoteAsync: cannot map ReportableEvent {ReportableEvent} to a query plan Frequency for facility {FacilityId} correlation {CorrelationId}; leaving {Count} pending reference id(s) in place for retry.",
                seedLog.ReportableEvent, facilityId, correlationId, pending.Count);
            return 0;
        }

        var queryPlan = await _queryPlanQueries.GetAsync(facilityId, planFrequency, cancellationToken);
        if (queryPlan == null)
        {
            _logger.LogWarning(
                "ReferentialPhasePromoter.PromoteAsync: no query plan found for facility {FacilityId} frequency {Frequency} (from ReportableEvent {ReportableEvent}); dropping {Count} pending reference id(s).",
                facilityId, planFrequency, seedLog.ReportableEvent, pending.Count);
            await PurgePendingAsync(facilityId, correlationId, cancellationToken);
            return 0;
        }

        // Build per-ResourceType groups paired with their ReferenceQueryConfig.
        var byType = pending
            .GroupBy(p => p.ResourceType, StringComparer.Ordinal)
            .Select(g => new
            {
                ResourceType = g.Key,
                Ids = g.Select(x => x.ResourceId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                Config = ResolveReferenceQueryConfig(queryPlan, g.Key),
            })
            .ToList();

        var resolvable = byType.Where(x => x.Config != null).ToList();
        var unresolvable = byType.Where(x => x.Config == null).ToList();

        foreach (var u in unresolvable)
        {
            _logger.LogWarning(
                "ReferentialPhasePromoter.PromoteAsync: reference type {ResourceType} has no ReferenceQueryConfig in facility {FacilityId} plan; dropping {Count} staged id(s).",
                u.ResourceType, facilityId, u.Ids.Count);
        }

        if (resolvable.Count == 0)
        {
            await PurgePendingAsync(facilityId, correlationId, cancellationToken);
            return 0;
        }

        // Transactional promotion. Inside the transaction we re-check idempotency so
        // a concurrent promoter running for the same correlation (e.g. the reactive
        // path racing the janitor sweep) can't create duplicate referential logs.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            if (await ReferentialLogExistsAsync(facilityId, correlationId, cancellationToken))
            {
                await PurgePendingAsync(facilityId, correlationId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }

            var siblingCount = resolvable.Count;
            var now = DateTime.UtcNow;
            int created = 0;

            foreach (var group in resolvable)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Enum.TryParse<ResourceType>(group.ResourceType, ignoreCase: false, out var parsedResourceType))
                {
                    _logger.LogWarning(
                        "ReferentialPhasePromoter.PromoteAsync: reference type {ResourceType} is not a valid FHIR ResourceType; skipping {Count} staged id(s).",
                        group.ResourceType, group.Ids.Count);
                    continue;
                }

                var fhirQueryType = MapOperationType(group.Config!.OperationType);
                var paged = group.Config.Paged > 0 ? group.Config.Paged : (int?)null;

                // Persist the batched ids as a single "_id=a,b,c" entry on QueryParameters.
                // The FhirQuery entity stores ids only via QueryParameters; the read-side
                // FhirQueryModel.IdQueryParameterValues is rehydrated from this row by
                // DataAcquisitionLogQueries.GetAsync for the executor to consume.
                var idsParam = $"_id={string.Join(',', group.Ids)}";

                var createModel = new CreateDataAcquisitionLogModel
                {
                    FacilityId = facilityId,
                    CorrelationId = correlationId,
                    ReportTrackingId = seedLog.ReportTrackingId?.ToString(),
                    ReportableEvent = seedLog.ReportableEvent,
                    Priority = seedLog.Priority,
                    PatientId = null,
                    FhirVersion = "R4",
                    QueryType = fhirQueryType,
                    QueryPhase = QueryPhase.Referential,
                    Status = RequestStatus.Pending,
                    ExecutionDate = now,
                    TraceId = seedLog.TraceId,
                    SiblingCount = siblingCount,
                    FhirQuery = new List<CreateFhirQueryModel>
                    {
                        new CreateFhirQueryModel
                        {
                            FacilityId = facilityId,
                            IsReference = true,
                            QueryType = fhirQueryType,
                            Paged = paged,
                            ResourceTypes = new List<ResourceType> { parsedResourceType },
                            QueryParameters = new List<string> { idsParam },
                        }
                    },
                };

                await _dataAcquisitionLogManager.CreateAsync(createModel, cancellationToken);
                created++;
            }

            await PurgePendingAsync(facilityId, correlationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "ReferentialPhasePromoter.PromoteAsync: promoted {Count} reference type(s) for facility {FacilityId} correlation {CorrelationId}.",
                created, facilityId, correlationId);

            return created;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ---------- helpers ----------

    private async Task<bool> IsInitialPhaseTerminalAsync(string facilityId, string correlationId, CancellationToken cancellationToken)
    {
        // Must have at least one Initial-phase log AND no non-terminal Initial-phase logs.
        var anyInitial = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .AnyAsync(l => l.FacilityId == facilityId
                        && l.CorrelationId == correlationId
                        && l.QueryPhase == QueryPhase.Initial
                        && !l.IsDeleted,
                cancellationToken);

        if (!anyInitial)
            return false;

        var anyNonTerminal = await _dbContext.DataAcquisitionLogs.AsNoTracking()
            .AnyAsync(l => l.FacilityId == facilityId
                        && l.CorrelationId == correlationId
                        && l.QueryPhase == QueryPhase.Initial
                        && !l.IsDeleted
                        && (l.Status == null || !TerminalStatuses.Contains(l.Status.Value)),
                cancellationToken);

        return !anyNonTerminal;
    }

    private Task<bool> ReferentialLogExistsAsync(string facilityId, string correlationId, CancellationToken cancellationToken)
    {
        return _dbContext.DataAcquisitionLogs.AsNoTracking()
            .AnyAsync(l => l.FacilityId == facilityId
                        && l.CorrelationId == correlationId
                        && l.QueryPhase == QueryPhase.Referential,
                cancellationToken);
    }

    private Task<int> PurgePendingAsync(string facilityId, string correlationId, CancellationToken cancellationToken)
    {
        return _dbContext.PendingReferenceIds
            .Where(p => p.FacilityId == facilityId && p.CorrelationId == correlationId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static ReferenceQueryConfig? ResolveReferenceQueryConfig(LantanaGroup.Link.DataAcquisition.Domain.Application.Models.QueryPlanModel plan, string resourceType)
    {
        // Reference queries can live in either Initial or Supplemental buckets of the
        // plan. Either bucket that declares a matching ResourceType wins; if both do
        // the Initial bucket takes precedence (matches historical semantics).
        if (plan.InitialQueries != null &&
            TryFindReferenceConfig(plan.InitialQueries, resourceType, out var fromInitial))
        {
            return fromInitial;
        }

        if (plan.SupplementalQueries != null &&
            TryFindReferenceConfig(plan.SupplementalQueries, resourceType, out var fromSupplemental))
        {
            return fromSupplemental;
        }

        return null;
    }

    private static bool TryFindReferenceConfig(
        IDictionary<string, LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces.IQueryConfig> bucket,
        string resourceType,
        out ReferenceQueryConfig? config)
    {
        foreach (var kvp in bucket)
        {
            if (kvp.Value is ReferenceQueryConfig rc &&
                string.Equals(rc.ResourceType, resourceType, StringComparison.Ordinal))
            {
                config = rc;
                return true;
            }
        }

        config = null;
        return false;
    }

    private static FhirQueryType MapOperationType(OperationType opType) =>
        opType switch
        {
            OperationType.Search => FhirQueryType.Search,
            OperationType.SearchPost => FhirQueryType.SearchPost,
            OperationType.Read => FhirQueryType.Read,
            _ => FhirQueryType.Search,
        };
}
