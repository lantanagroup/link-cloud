using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Stages discovered reference resource IDs for later collection by the reference log.
    /// Writes independent rows to PendingReferenceIds — no shared mutable state,
    /// no contention between concurrent patient workers.
    /// </summary>
    Task ProcessReferences(DataAcquisitionLogModel log, List<ResourceReference> refResources, CancellationToken cancellationToken = default);
}

public class ReferenceResourceService : IReferenceResourceService
{
    private readonly ILogger<ReferenceResourceService> _logger;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;
    private readonly DataAcquisitionDbContext _dbContext;

    // Per-scope cache for FindReferenceQueryAsync results.
    // The lookup is deterministic for the lifetime of a single log execution
    // (same facility + reportTrackingId + correlationId + resourceType always returns
    // the same FhirQueryId), so we avoid hitting the DB on every FHIR bundle page.
    private readonly Dictionary<string, ReferenceQueryLookupResult?> _referenceQueryCache = new();


    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesQueries referenceResourcesQueries,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        DataAcquisitionDbContext dbContext)
    {
        _logger = logger;
        _referenceResourcesQueries = referenceResourcesQueries;
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries;
        _dbContext = dbContext;
    }

    public async Task<List<Resource>> FetchReferenceResources(ReferenceQueryFactoryResult referenceQueryFactoryResult, GetPatientDataRequest request, FhirQueryConfigurationModel fhirQueryConfiguration, ReferenceQueryConfig referenceQueryConfig, string queryPlanType, CancellationToken cancellationToken = default)
    {
        var resources = new List<Resource>();
        if (referenceQueryFactoryResult.ReferenceIds?.Count == 0)
        {
            return resources;
        }

        var validReferenceResources =
            referenceQueryFactoryResult
            ?.ReferenceIds
            ?.Where(x => x.TypeName == referenceQueryConfig.ResourceType || x.Reference.StartsWith(referenceQueryConfig.ResourceType, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        var existingReferenceResources = (await _referenceResourcesQueries.SearchAsync(new SearchReferenceResourcesModel
        {
            FacilityId = request.FacilityId,
            ResourceIds = validReferenceResources.Select(x => x.Reference.SplitReference()).ToList(),
            PageSize = int.MaxValue
        })).Records;


        resources.AddRange(existingReferenceResources.Select(x => FhirResourceDeserializer.DeserializeFhirResource(x)));

        List<ResourceReference> missingReferences = validReferenceResources
            .Where(x => !existingReferenceResources.Any(y => y.ResourceId == x.Reference.SplitReference())).ToList();

        foreach (var x in missingReferences)
        {
            var fullMissingResources = new List<Resource>();
            resources.AddRange(fullMissingResources);
        }

        return resources;
    }


    public async Task ProcessReferences(DataAcquisitionLogModel log, List<ResourceReference> refResources, CancellationToken cancellationToken = default)
    {
        if (refResources == null || refResources.Count == 0)
            return;

        if (log == null)
            throw new ArgumentNullException(nameof(log), "Data acquisition log cannot be null.");

        var groupedIdentities = refResources.Select(rr => rr.Reference)
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => new ResourceIdentity(r))
            .GroupBy(i => i.ResourceType)
            .ToList();

        _logger.LogInformation("Processing {Count} reference resources for log with ID: {LogId}", groupedIdentities.Sum(g => g.Count()), log.Id);

        foreach (var group in groupedIdentities)
        {
            var resourceType = group.Key;
            if (string.IsNullOrEmpty(resourceType))
            {
                _logger.LogWarning("Skipping reference resources with no type for log with ID: {LogId}", log.Id);
                continue;
            }

            var cacheKey = $"{log.FacilityId}|{log.ReportTrackingId}|{log.CorrelationId}|{resourceType}";
            if (!_referenceQueryCache.TryGetValue(cacheKey, out var lookup))
            {
                lookup = await _dataAcquisitionLogQueries.FindReferenceQueryAsync(
                    log.FacilityId, log.ReportTrackingId, log.CorrelationId,
                    resourceType, cancellationToken);
                _referenceQueryCache[cacheKey] = lookup;
            }

            if (lookup == null)
            {
                throw new InvalidOperationException($"No data acquisition log for reference resource type: {resourceType}");
            }

            // Stage each discovered ID as an independent row — no shared mutable state.
            var newIds = group.Select(i => i.Id).Distinct().ToList();

            var existingIds = await _dbContext.PendingReferenceIds
                .Where(p => p.FhirQueryId == lookup.FhirQueryId
                    && p.ResourceType == resourceType
                    && newIds.Contains(p.ResourceId))
                .Select(p => p.ResourceId)
                .ToListAsync(cancellationToken);

            var existingIdSet = existingIds.ToHashSet(StringComparer.Ordinal);
            var entities = newIds
                .Where(id => !existingIdSet.Contains(id))
                .Select(id => new PendingReferenceId
            {
                FhirQueryId = lookup.FhirQueryId,
                ResourceId = id,
                ResourceType = resourceType
            });

            _dbContext.PendingReferenceIds.AddRange(entities);
        }

        // Single SaveChangesAsync for all resource type groups in this call
        if (_dbContext.ChangeTracker.HasChanges())
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogDebug("Duplicate PendingReferenceIds insert detected during SaveChangesAsync; ignoring as idempotent race.");

                foreach (var entry in _dbContext.ChangeTracker.Entries<PendingReferenceId>()
                             .Where(e => e.State == EntityState.Added))
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        return false;
    }
}
