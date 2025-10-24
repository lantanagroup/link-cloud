using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
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
    /// Processes a collection of resource references and updates the specified data acquisition log accordingly.
    /// </summary>
    /// <remarks>This method processes the provided resource references and updates the log with relevant
    /// information. Ensure that the <paramref name="refResources"/> list contains valid references before calling this
    /// method.</remarks>
    /// <param name="log">The data acquisition log to be updated. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="refResources">A list of resource references to process. This parameter can be <see langword="null"/> if no references were found.</param>
    /// <param name="cancellationToken">An optional token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessReferences(DataAcquisitionLogModel log, List<ResourceReference> refResources, CancellationToken cancellationToken = default);
}

public class ReferenceResourceService : IReferenceResourceService
{
    private readonly ILogger<ReferenceResourceService> _logger;
    private readonly IReferenceResourcesManager _referenceResourcesManager;
    private readonly IReferenceResourcesQueries _referenceResourcesQueries;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;
    private readonly IFhirQueryManager _fhirQueryMananger;


    public ReferenceResourceService(
        ILogger<ReferenceResourceService> logger,
        IReferenceResourcesManager referenceResourcesManager,
        IReferenceResourcesQueries referenceResourcesQueries,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IDataAcquisitionServiceMetrics metrics,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        IFhirQueryManager fhirQueryMananger)
    {
        _logger = logger;
        _referenceResourcesManager = referenceResourcesManager;
        _referenceResourcesQueries = referenceResourcesQueries;
        _kafkaProducer = kafkaProducer;
        _metrics = metrics;
        _dataAcquisitionLogManager = dataAcquisitionLogManager;
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries;
        _fhirQueryMananger = fhirQueryMananger;
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
                _logger.LogWarning("Skipping reference resources with no type for log with ID: {LodId}", log.Id);
                continue;
            }

            var referenceLog = (await _dataAcquisitionLogQueries.SearchAsync(new SearchDataAcquisitionLogRequest
            {
                FacilityId = log.FacilityId,
                ReportTrackingId = log.ReportTrackingId,
                CorrelationId = log.CorrelationId,
                ResourceType = resourceType,
                PageSize = int.MaxValue
            }, cancellationToken)).Records.FirstOrDefault();
           
            if (referenceLog == null)
            {
                throw new InvalidOperationException($"No data acquisition log for reference resource type: {resourceType}");
            }
            if (referenceLog.FhirQuery == null || referenceLog.FhirQuery.Count == 0)
            {
                throw new InvalidOperationException($"No FHIR query for reference resource type: {resourceType}");
            }
            if (referenceLog.FhirQuery.Count > 1)
            {
                throw new InvalidOperationException($"Multiple FHIR queries for reference resource type: {resourceType}");
            }
            var fhirQuery = referenceLog.FhirQuery.First();

            fhirQuery.IdQueryParameterValues = fhirQuery.IdQueryParameterValues.ToList()
                .Concat(group.Select(i => i.Id))
                .Distinct().ToList();

            await _fhirQueryMananger.UpdateAsync(fhirQuery, cancellationToken);
        }
    }
}
