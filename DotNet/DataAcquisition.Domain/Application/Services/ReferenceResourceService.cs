using System.Text;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using IsolationLevel = System.Data.IsolationLevel;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IReferenceResourceService
{
    Task<List<Resource>> FetchReferenceResources(
        ReferenceQueryFactoryResult referenceQueryFactoryResult,
        GetPatientDataRequest request,
        FhirQueryConfigurationModel fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default);

    Task<int> FetchAndPersistAsync(
        DataAcquisitionLogModel primaryLog,
        DiscoveredReferenceAccumulator accumulator,
        CancellationToken cancellationToken = default);

    Task<PreparedReferenceLogExecutionModel> PrepareReferenceLogExecutionAsync(
        DataAcquisitionLogModel log,
        DiscoveredReferenceAccumulator? accumulator = null,
        CancellationToken cancellationToken = default);
}

public class ReferenceResourceService : IReferenceResourceService
{
    private readonly ILogger<ReferenceResourceService> _logger;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;
    private readonly IReferenceResourcesManager _referenceResourcesManager;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IQueryPlanQueries _queryPlanQueries;
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly IResourceCache _resourceCache;

    private const int LockTimeoutMs = 30000;

    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesQueries referenceResourcesQueries,
        IReferenceResourcesManager referenceResourcesManager,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IQueryPlanQueries queryPlanQueries,
        DataAcquisitionDbContext dbContext,
        IResourceCache resourceCache)
    {
        _logger = logger;
        _referenceResourcesQueries = referenceResourcesQueries;
        _referenceResourcesManager = referenceResourcesManager;
        _dataAcquisitionLogManager = dataAcquisitionLogManager;
        _queryPlanQueries = queryPlanQueries;
        _dbContext = dbContext;
        _resourceCache = resourceCache;
    }

    public async Task<List<Resource>> FetchReferenceResources(
        ReferenceQueryFactoryResult referenceQueryFactoryResult,
        GetPatientDataRequest request,
        FhirQueryConfigurationModel fhirQueryConfiguration,
        ReferenceQueryConfig referenceQueryConfig,
        string queryPlanType,
        CancellationToken cancellationToken = default)
    {
        var resources = new List<Resource>();
        if (referenceQueryFactoryResult.ReferenceIds?.Count == 0)
        {
            return resources;
        }

        var validReferenceResources =
            (referenceQueryFactoryResult.ReferenceIds ?? Enumerable.Empty<ResourceReference>())
            .Where(x => x.TypeName == referenceQueryConfig.ResourceType || x.Reference.StartsWith(referenceQueryConfig.ResourceType, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        var referenceIds = validReferenceResources.Select(x => x.Reference.SplitReference()).ToList();
        var existingReferenceResources = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
        {
            FacilityId = request.FacilityId,
            ResourceIds = referenceIds,
            PageSize = int.MaxValue
        }, cancellationToken)).Records;

        resources.AddRange(existingReferenceResources.Select(FhirResourceDeserializer.DeserializeFhirResource));

        return resources;
    }

    public async Task<int> FetchAndPersistAsync(
        DataAcquisitionLogModel primaryLog,
        DiscoveredReferenceAccumulator accumulator,
        CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("ReferenceResourceService.FetchAndPersistAsync");

        if (primaryLog == null) throw new ArgumentNullException(nameof(primaryLog));
        if (accumulator == null) throw new ArgumentNullException(nameof(accumulator));
        if (string.IsNullOrWhiteSpace(primaryLog.FacilityId))
            throw new ArgumentException("Primary log is missing a FacilityId.", nameof(primaryLog));
        if (string.IsNullOrWhiteSpace(primaryLog.CorrelationId))
            throw new ArgumentException("Primary log is missing a CorrelationId.", nameof(primaryLog));
        if (primaryLog.QueryPhase == null)
            throw new ArgumentException("Primary log is missing a QueryPhase.", nameof(primaryLog));
        if (primaryLog.ReportableEvent == null)
            throw new ArgumentException("Primary log is missing a ReportableEvent.", nameof(primaryLog));

        if (!accumulator.HasAny)
            return 0;

        activity?.SetTag(DiagnosticNames.FacilityId, primaryLog.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, primaryLog.CorrelationId);
        activity?.SetTag(DiagnosticNames.DataAcquisitionLogId, primaryLog.Id);

        // Resolve the facility's query plan once. The plan's per-type ReferenceQueryConfig
        // tells us OperationType (Search vs SearchPost) and Paged batch size.
        Frequency planFrequency;
        try
        {
            planFrequency = ReportableEventToQueryPlanTypeFactory
                .GenerateQueryPlanTypeFromReportableEvent(primaryLog.ReportableEvent.Value);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex,
                "FetchAndPersistAsync: cannot map ReportableEvent {ReportableEvent} to a Frequency for facility {FacilityId} correlation {CorrelationId}; dropping {Count} discovered reference type(s).",
                primaryLog.ReportableEvent.SanitizeForLog(), primaryLog.FacilityId.SanitizeForLog(), primaryLog.CorrelationId.SanitizeForLog(), accumulator.ByType.Count.SanitizeForLog());
            return 0;
        }

        var queryPlan = await _queryPlanQueries.GetAsync(primaryLog.FacilityId, planFrequency, cancellationToken);
        if (queryPlan == null)
        {
            _logger.LogWarning(
                "FetchAndPersistAsync: no query plan found for facility {FacilityId} frequency {Frequency}; dropping {Count} discovered reference type(s).",
                primaryLog.FacilityId.SanitizeForLog(), planFrequency.SanitizeForLog(), accumulator.ByType.Count.SanitizeForLog());
            return 0;
        }

        var pendingReferenceIdsAdded = 0;

        foreach (var (resourceType, idSet) in accumulator.ByType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (idSet == null || idSet.Count == 0)
                continue;

            var refConfig = ResolveReferenceQueryConfig(queryPlan, resourceType);
            if (refConfig == null)
            {
                _logger.LogDebug(
                    "FetchAndPersistAsync: no ReferenceQueryConfig for type {ResourceType} in facility {FacilityId} plan; skipping {Count} discovered id(s).",
                    resourceType.SanitizeForLog(), primaryLog.FacilityId.SanitizeForLog(), idSet.Count.SanitizeForLog());
                continue;
            }

            if (!Enum.TryParse<ResourceType>(resourceType, ignoreCase: false, out var parsedResourceType))
            {
                _logger.LogWarning(
                    "FetchAndPersistAsync: type {ResourceType} is not a valid FHIR ResourceType; skipping {Count} id(s).",
                    resourceType, idSet.Count);
                continue;
            }

            var requestedIds = idSet
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (requestedIds.Count == 0)
                continue;

            pendingReferenceIdsAdded += await GetOrCreateAndAppendAsync(
                primaryLog,
                parsedResourceType,
                refConfig,
                requestedIds,
                new List<CreateResourceReferenceTypeModel>
                {
                    new CreateResourceReferenceTypeModel
                    {
                        FacilityId = primaryLog.FacilityId,
                        QueryPhase = primaryLog.QueryPhase.GetValueOrDefault(),
                        ResourceType = resourceType,
                    }
                },
                cancellationToken);
        }

        return pendingReferenceIdsAdded;
    }

    public async Task<PreparedReferenceLogExecutionModel> PrepareReferenceLogExecutionAsync(
        DataAcquisitionLogModel log,
        DiscoveredReferenceAccumulator? accumulator = null,
        CancellationToken cancellationToken = default)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));

        var resourceType = ResolveReferenceResourceType(log);
        if (string.IsNullOrWhiteSpace(resourceType))
        {
            return new PreparedReferenceLogExecutionModel
            {
                SkipFetch = true,
                Notes = new[] { $"[{DateTime.UtcNow:O}] Reference log has no reference resource type; skipping execution." }
            };
        }

        var pendingIds = await _dbContext.PendingReferenceIds
            .AsNoTracking()
            .Where(p => p.DataAcquisitionLogId == log.Id)
            .Select(p => p.ResourceId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

        // Persist the full pending set as the log's query of record.
        if (pendingIds.Count > 0)
        {
            await PersistReferenceQueryParametersAsync(log.Id, pendingIds, cancellationToken);
        }

        var acquiredIds = (log.ResourceAcquiredIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var alreadyAcquiredBareIds = acquiredIds
            .Where(id => id.StartsWith($"{resourceType}/", StringComparison.OrdinalIgnoreCase))
            .Select(id => id.SplitReference())
            .ToHashSet(StringComparer.Ordinal);

        var outstandingIds = pendingIds
            .Where(id => !alreadyAcquiredBareIds.Contains(id))
            .ToList();

        if (outstandingIds.Count == 0)
        {
            return new PreparedReferenceLogExecutionModel
            {
                SkipFetch = true,
                ResourceIds = acquiredIds,
                Notes = new[] { $"[{DateTime.UtcNow:O}] No outstanding reference ids remained for {resourceType}; completing without FHIR fetch." }
            };
        }

        var cachedRows = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
        {
            FacilityId = log.FacilityId,
            ResourceType = resourceType,
            ResourceIds = outstandingIds,
            PageSize = int.MaxValue
        }, cancellationToken)).Records;

        var cachedById = cachedRows
            .GroupBy(r => r.ResourceId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Reference logs surface their own resources via ResourceAcquiredIds; the
        // junction is reserved for primary logs that depend on them.
        var resourceIds = new HashSet<string>(acquiredIds, StringComparer.Ordinal);
        foreach (var cached in cachedRows)
        {
            Resource? resource;
            try
            {
                resource = FhirResourceDeserializer.DeserializeFhirResource(cached);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PrepareReferenceLogExecutionAsync: failed to deserialize cached reference {ResourceType}/{ResourceId} for log {LogId}.",
                    resourceType.SanitizeForLog(), cached.ResourceId.SanitizeForLog(), log.Id.SanitizeForLog());
                continue;
            }

            if (resource == null)
                continue;

            resourceIds.Add($"{resource.TypeName}/{resource.Id}");
            AddResourceToCache(log, resource);

            // Extract and accumulate nested references from cached resources
            if (accumulator != null && log.FhirQuery.Any())
            {
                var referenceTypes = log.FhirQuery
                    .SelectMany(q => q.ResourceReferenceTypes.Select(rt => rt.ResourceType))
                    .Distinct()
                    .ToList();
                var refResources = ReferenceResourceBundleExtractor.Extract(resource, referenceTypes);
                if (refResources.Any())
                {
                    AccumulateDiscoveredReferences(refResources, accumulator);
                }
            }
        }

        var missingIds = outstandingIds.Where(id => !cachedById.ContainsKey(id)).ToList();
        if (missingIds.Count == 0)
        {
            return new PreparedReferenceLogExecutionModel
            {
                SkipFetch = true,
                ResourceIds = resourceIds.ToList(),
                Notes = new[] { $"[{DateTime.UtcNow:O}] Reference ids for {resourceType} were satisfied from canonical cache without a FHIR round-trip." }
            };
        }

        // In-memory only: the on-the-wire call fetches just the cache-miss subset.
        foreach (var fhirQuery in log.FhirQuery.Where(q => q.IsReference.GetValueOrDefault()))
        {
            fhirQuery.IdQueryParameterValues = missingIds;
        }

        var notes = cachedRows.Count > 0
            ? new[] { $"[{DateTime.UtcNow:O}] {cachedRows.Count} reference resource(s) for {resourceType} were served from canonical cache; fetching {missingIds.Count} remaining id(s) from FHIR." }
            : Array.Empty<string>();

        return new PreparedReferenceLogExecutionModel
        {
            SkipFetch = false,
            ResourceIds = resourceIds.ToList(),
            Notes = notes
        };
    }

    private async Task<int> GetOrCreateAndAppendAsync(
        DataAcquisitionLogModel primaryLog,
        ResourceType resourceType,
        ReferenceQueryConfig refConfig,
        List<string> resourceIds,
        List<CreateResourceReferenceTypeModel> resourceReferenceTypes,
        CancellationToken cancellationToken)
    {
        bool created = false;
        long referenceLogId = 0;
        var pendingReferenceIdsAdded = 0;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async (ct) =>
        {
            _dbContext.ChangeTracker.Clear();
            created = false;
            pendingReferenceIdsAdded = 0;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await AcquireLockAsync(primaryLog, resourceType, ct);

            try
            {
                var existingLogId = await _dbContext.DataAcquisitionLogs
                    .AsNoTracking()
                    .Where(l => l.FacilityId == primaryLog.FacilityId
                        && l.CorrelationId == primaryLog.CorrelationId
                        && l.QueryPhase == primaryLog.QueryPhase
                        && l.ReferenceResourceType == resourceType.ToString())
                    .Select(l => (long?)l.Id)
                    .FirstOrDefaultAsync(ct);

                if (existingLogId.HasValue)
                {
                    referenceLogId = existingLogId.Value;
                }
                else
                {
                    var fhirQueryType = MapOperationType(refConfig.OperationType);
                    var pageSize = refConfig.Paged > 0 ? (int?)refConfig.Paged : null;

                    var createdLog = await _dataAcquisitionLogManager.CreateAsync(new CreateDataAcquisitionLogModel
                    {
                        FacilityId = primaryLog.FacilityId,
                        CorrelationId = primaryLog.CorrelationId,
                        ReportTrackingId = primaryLog.ReportTrackingId,
                        ReportableEvent = primaryLog.ReportableEvent,
                        Priority = primaryLog.Priority,
                        PatientId = primaryLog.PatientId,
                        ReferenceResourceType = resourceType.ToString(),
                        FhirVersion = "R4",
                        QueryType = fhirQueryType,
                        QueryPhase = primaryLog.QueryPhase,
                        Status = RequestStatus.Pending,
                        ExecutionDate = DateTime.UtcNow,
                        TraceId = primaryLog.TraceId,
                        Notes = new List<string>
                        {
                            $"Reference aggregation log for {resourceType} created from primary correlation discovery."
                        },
                        FhirQuery = new List<CreateFhirQueryModel>
                        {
                            new CreateFhirQueryModel
                            {
                                FacilityId = primaryLog.FacilityId,
                                IsReference = true,
                                QueryType = fhirQueryType,
                                Paged = pageSize,
                                ResourceTypes = new List<ResourceType> { resourceType },
                                QueryParameters = new List<string>(),
                                ResourceReferenceTypes = resourceReferenceTypes ?? new List<CreateResourceReferenceTypeModel>()
                            }
                        }
                    }, ct);

                    referenceLogId = createdLog.Id;
                    created = true;

                    // Stamp ONLY the new reference log's SiblingCount with a single-row
                    // UPDATE so it participates in TryCompleteTailAsync's count-based tail
                    // gate. We deliberately avoid the previous wide-range UPDATE over every
                    // log in the (facility, correlation, phase) group: that wide UPDATE
                    // deadlocked with TryCompleteTailAsync's wide TailSent UPDATE on the
                    // same range. The exact stamped value is not significant for the tail
                    // threshold (which now COUNTs stamped siblings instead of trusting one
                    // row's value); we use the post-insert sibling count purely for human
                    // readability when inspecting the row.
                    var stampedSiblingCount = await _dbContext.DataAcquisitionLogs
                        .AsNoTracking()
                        .CountAsync(
                            l => l.FacilityId == primaryLog.FacilityId
                                && l.CorrelationId == primaryLog.CorrelationId
                                && l.QueryPhase == primaryLog.QueryPhase
                                && l.SiblingCount != null,
                            ct);

                    await _dbContext.DataAcquisitionLogs
                        .Where(l => l.Id == referenceLogId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(l => l.SiblingCount, stampedSiblingCount + 1)
                            .SetProperty(l => l.ModifyDate, DateTime.UtcNow),
                            ct);
                }

                var existingPendingIds = await _dbContext.PendingReferenceIds
                    .AsNoTracking()
                    .Where(p => p.DataAcquisitionLogId == referenceLogId)
                    .Select(p => p.ResourceId)
                    .ToListAsync(ct);

                var existingAcquiredIds = await _dbContext.DataAcquisitionLogResourceIds
                    .AsNoTracking()
                    .Where(r => r.DataAcquisitionLogId == referenceLogId
                        && r.ResourceId.StartsWith(resourceType + "/"))
                    .Select(r => r.ResourceId)
                    .ToListAsync(ct);

                var acquiredBareIds = existingAcquiredIds
                    .Select(id => id.SplitReference())
                    .ToHashSet(StringComparer.Ordinal);

                var pendingLookup = existingPendingIds.ToHashSet(StringComparer.Ordinal);
                var toInsert = resourceIds
                    .Where(id => !pendingLookup.Contains(id) && !acquiredBareIds.Contains(id))
                    .Select(id => new PendingReferenceId
                    {
                        DataAcquisitionLogId = referenceLogId,
                        FacilityId = primaryLog.FacilityId,
                        CorrelationId = primaryLog.CorrelationId!,
                        ResourceType = resourceType.ToString(),
                        ResourceId = id,
                        CreateDate = DateTime.UtcNow
                    })
                    .ToList();

                if (toInsert.Count > 0)
                {
                    _dbContext.PendingReferenceIds.AddRange(toInsert);
                    await _dbContext.SaveChangesAsync(ct);
                    pendingReferenceIdsAdded = toInsert.Count;
                }

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }, cancellationToken);

        if (created)
        {
            _logger.LogDebug(
                "FetchAndPersistAsync: created reference log {ReferenceLogId} for {FacilityId}/{CorrelationId}/{QueryPhase}/{ResourceType}.",
                referenceLogId.SanitizeForLog(), primaryLog.FacilityId.SanitizeForLog(), primaryLog.CorrelationId.SanitizeForLog(), primaryLog.QueryPhase.SanitizeForLog(), resourceType.SanitizeForLog());
        }

        return pendingReferenceIdsAdded;
    }

    private void AddResourceToCache(
        DataAcquisitionLogModel primaryLog,
        Resource resource)
    {
        if (resource is DomainResource domainResource
            && !string.IsNullOrWhiteSpace(resource.TypeName)
            && Enum.TryParse<ResourceType>(resource.TypeName, out var resourceType))
        {
            _resourceCache.UpdateCorrelationCache($"{primaryLog.CorrelationId}:{resourceType}", new List<DomainResource> { domainResource }, resourceType);
        }
    }

    private async Task PersistReferenceQueryParametersAsync(
        long dataAcquisitionLogId,
        IReadOnlyList<string> pendingIds,
        CancellationToken cancellationToken)
    {
        var referenceQueries = await _dbContext.FhirQueries
            .Where(q => q.DataAcquisitionLogId == dataAcquisitionLogId
                && q.IsReference == true)
            .ToListAsync(cancellationToken);

        if (referenceQueries.Count == 0)
            return;

        var changed = false;
        foreach (var fhirQuery in referenceQueries)
        {
            var before = string.Join('|', fhirQuery.QueryParameters ?? new List<string>());
            fhirQuery.IdQueryParameterValues = pendingIds;
            var after = string.Join('|', fhirQuery.QueryParameters ?? new List<string>());

            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                fhirQuery.ModifyDate = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var fhirQuery in referenceQueries)
        {
            _dbContext.Entry(fhirQuery).State = EntityState.Detached;
        }
    }

    private string ResolveReferenceResourceType(DataAcquisitionLogModel log)
    {
        if (!string.IsNullOrWhiteSpace(log.ReferenceResourceType))
            return log.ReferenceResourceType;

        return log.FhirQuery
            .SelectMany(q => q.ResourceTypes)
            .Select(r => r.ToString())
            .FirstOrDefault() ?? string.Empty;
    }

    private async Task AcquireLockAsync(
        DataAcquisitionLogModel primaryLog,
        ResourceType resourceType,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = @"
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @lockTimeoutMs,
                @DbPrincipal = 'public';
            SELECT @result;";

        var resourceParam = command.CreateParameter();
        resourceParam.ParameterName = "@resource";
        // The lock scope is the (facility, correlation, phase) group, NOT per-resource-type.
        // Broader serialization is required because GetOrCreateAndAppendAsync also performs
        // a single-row UPDATE that stamps the new ref log's SiblingCount based on a live
        // count of stamped siblings. Per-type locking would let two transactions read the
        // same count and assign duplicate stamps; a single per-group lock makes the
        // read-then-stamp sequence linearizable across resource types within the group.
        // The throughput cost is negligible because ref-log work per group is fast and
        // only happens when primaries finish.
        resourceParam.Value = $"ReferenceLog:{primaryLog.FacilityId}:{primaryLog.CorrelationId}:{primaryLog.QueryPhase}";
        command.Parameters.Add(resourceParam);

        var timeoutParam = command.CreateParameter();
        timeoutParam.ParameterName = "@lockTimeoutMs";
        timeoutParam.Value = LockTimeoutMs;
        command.Parameters.Add(timeoutParam);

        var dbTransaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (dbTransaction == null)
            throw new InvalidOperationException("AcquireLockAsync must be called within an active transaction.");

        command.Transaction = dbTransaction;

        var resultObj = await command.ExecuteScalarAsync(cancellationToken);
        var result = Convert.ToInt32(resultObj);
        if (result < 0)
            throw new InvalidOperationException($"Unable to acquire SQL app lock for reference-log group '{primaryLog.FacilityId}:{primaryLog.CorrelationId}:{primaryLog.QueryPhase}' (resourceType='{resourceType}'). sp_getapplock result={result}.");
    }

    private static ReferenceQueryConfig? ResolveReferenceQueryConfig(QueryPlanModel plan, string resourceType)
    {
        if (plan.InitialQueries != null
            && TryFindReferenceConfig(plan.InitialQueries, resourceType, out var fromInitial))
        {
            return fromInitial;
        }

        if (plan.SupplementalQueries != null
            && TryFindReferenceConfig(plan.SupplementalQueries, resourceType, out var fromSupplemental))
        {
            return fromSupplemental;
        }

        return null;
    }

    private void AccumulateDiscoveredReferences(
        IReadOnlyList<ResourceReference> refResources,
        DiscoveredReferenceAccumulator accumulator)
    {
        if (refResources == null || refResources.Count == 0)
            return;

        foreach (var rr in refResources)
        {
            if (string.IsNullOrWhiteSpace(rr?.Reference))
                continue;

            var identity = new ResourceIdentity(rr.Reference);
            if (string.IsNullOrWhiteSpace(identity.ResourceType) || string.IsNullOrWhiteSpace(identity.Id))
                continue;

            accumulator.Add(identity.ResourceType, identity.Id);
        }
    }

    private static bool TryFindReferenceConfig(
        IDictionary<string, IQueryConfig> bucket,
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

