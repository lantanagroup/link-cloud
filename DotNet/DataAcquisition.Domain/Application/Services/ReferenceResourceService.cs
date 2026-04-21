using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
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
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

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

    Task FetchAndPersistAsync(
        DataAcquisitionLogModel primaryLog,
        DiscoveredReferenceAccumulator accumulator,
        CancellationToken cancellationToken = default);

    Task<PreparedReferenceLogExecutionModel> PrepareReferenceLogExecutionAsync(
        DataAcquisitionLogModel log,
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
    private readonly IProducer<ResourceKey, ResourceAcquired> _kafkaProducer;

    private const int LockTimeoutMs = 30000;

    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesQueries referenceResourcesQueries,
        IReferenceResourcesManager referenceResourcesManager,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IQueryPlanQueries queryPlanQueries,
        DataAcquisitionDbContext dbContext,
        IProducer<ResourceKey, ResourceAcquired> kafkaProducer)
    {
        _logger = logger;
        _referenceResourcesQueries = referenceResourcesQueries;
        _referenceResourcesManager = referenceResourcesManager;
        _dataAcquisitionLogManager = dataAcquisitionLogManager;
        _queryPlanQueries = queryPlanQueries;
        _dbContext = dbContext;
        _kafkaProducer = kafkaProducer;
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
            referenceQueryFactoryResult
            .ReferenceIds
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

    public async Task FetchAndPersistAsync(
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
            return;

        activity?.SetTag(DiagnosticNames.FacilityId, primaryLog.FacilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, primaryLog.CorrelationId);
        activity?.SetTag(DiagnosticNames.ReportId, primaryLog.Id);

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
                primaryLog.ReportableEvent, primaryLog.FacilityId, primaryLog.CorrelationId, accumulator.ByType.Count);
            return;
        }

        var queryPlan = await _queryPlanQueries.GetAsync(primaryLog.FacilityId, planFrequency, cancellationToken);
        if (queryPlan == null)
        {
            _logger.LogWarning(
                "FetchAndPersistAsync: no query plan found for facility {FacilityId} frequency {Frequency}; dropping {Count} discovered reference type(s).",
                primaryLog.FacilityId, planFrequency, accumulator.ByType.Count);
            return;
        }

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
                    resourceType, primaryLog.FacilityId, idSet.Count);
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

            await GetOrCreateAndAppendAsync(
                primaryLog,
                parsedResourceType,
                refConfig,
                requestedIds,
                cancellationToken);
        }
    }

    public async Task<PreparedReferenceLogExecutionModel> PrepareReferenceLogExecutionAsync(
        DataAcquisitionLogModel log,
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

        var cacheRowIds = cachedRows.Select(r => r.Id).Distinct().ToList();
        if (cacheRowIds.Count > 0)
        {
            await _referenceResourcesManager.LinkToLogAsync(log.Id, cacheRowIds, cancellationToken);
        }

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
                    resourceType, cached.ResourceId, log.Id);
                continue;
            }

            if (resource == null)
                continue;

            resourceIds.Add($"{resource.TypeName}/{resource.Id}");
            await PublishResourceAcquiredAsync(log, resource, cancellationToken);
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

        foreach (var fhirQuery in log.FhirQuery.Where(q => q.IsReference.GetValueOrDefault()))
        {
            // IdQueryParameterValues is a computed view over QueryParameters: the
            // setter strips any existing "_id=" entries and re-appends a single
            // "_id=a,b,c" entry, so QueryParameters stays the single source of truth.
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

    private async Task GetOrCreateAndAppendAsync(
        DataAcquisitionLogModel primaryLog,
        ResourceType resourceType,
        ReferenceQueryConfig refConfig,
        List<string> resourceIds,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        await AcquireLockAsync(primaryLog, resourceType, cancellationToken);

        try
        {
            var existingLogId = await _dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .Where(l => l.FacilityId == primaryLog.FacilityId
                    && l.CorrelationId == primaryLog.CorrelationId
                    && l.QueryPhase == primaryLog.QueryPhase
                    && l.ReferenceResourceType == resourceType.ToString())
                .Select(l => (long?)l.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var created = false;
            long referenceLogId;

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
                    PatientId = null,
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
                            QueryParameters = new List<string>()
                        }
                    }
                }, cancellationToken);

                referenceLogId = createdLog.Id;
                created = true;

                await _dataAcquisitionLogManager.IncrementSiblingCountAsync(
                    primaryLog.FacilityId,
                    primaryLog.CorrelationId!,
                    primaryLog.QueryPhase!.Value,
                    delta: 1,
                    cancellationToken);
            }

            var existingPendingIds = await _dbContext.PendingReferenceIds
                .AsNoTracking()
                .Where(p => p.DataAcquisitionLogId == referenceLogId)
                .Select(p => p.ResourceId)
                .ToListAsync(cancellationToken);

            var existingAcquiredIds = await _dbContext.DataAcquisitionLogResourceIds
                .AsNoTracking()
                .Where(r => r.DataAcquisitionLogId == referenceLogId
                    && r.ResourceId.StartsWith(resourceType + "/"))
                .Select(r => r.ResourceId)
                .ToListAsync(cancellationToken);

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
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            if (created)
            {
                _logger.LogDebug(
                    "FetchAndPersistAsync: created reference log {ReferenceLogId} for {FacilityId}/{CorrelationId}/{QueryPhase}/{ResourceType}.",
                    referenceLogId, primaryLog.FacilityId, primaryLog.CorrelationId, primaryLog.QueryPhase, resourceType);
            }
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task PublishResourceAcquiredAsync(
        DataAcquisitionLogModel primaryLog,
        Resource resource,
        CancellationToken cancellationToken)
    {
        var headers = new Headers
        {
            new Header(DataAcquisitionConstants.HeaderNames.CorrelationId,
                Encoding.UTF8.GetBytes(primaryLog.CorrelationId!))
        };

        await _kafkaProducer.ProduceAsync(
            KafkaTopic.ResourceAcquired.ToString(),
            new Message<ResourceKey, ResourceAcquired>
            {
                Key = new ResourceKey
                {
                    FacilityId = primaryLog.FacilityId,
                    CorrelationId = primaryLog.CorrelationId
                },
                Headers = headers,
                Value = new ResourceAcquired
                {
                    Resource = resource,
                    ResourceType = resource.TypeName,
                    ScheduledReports = primaryLog.ScheduledReport != null
                        ? new List<ScheduledReport> { primaryLog.ScheduledReport }
                        : new List<ScheduledReport>(),
                    PatientId = null,
                    QueryType = QueryPhaseUtilities.ToWireQueryType(primaryLog.QueryPhase),
                    ReportableEvent = primaryLog.ReportableEvent
                        ?? throw new ArgumentNullException(nameof(primaryLog.ReportableEvent))
                }
            },
            cancellationToken);
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
        // Broader serialization is required because GetOrCreateAndAppendAsync also calls
        // IncrementSiblingCountAsync, which performs a wide UPDATE over every log row in
        // the group. Two transactions holding different per-type app locks would each
        // try to lock the other's newly-inserted ref-log row inside that wide UPDATE,
        // producing a SQL Server deadlock. Locking at the group level removes the cross-
        // transaction contention entirely; the throughput cost is negligible because
        // ref-log work per group is fast and only happens when primaries finish.
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

