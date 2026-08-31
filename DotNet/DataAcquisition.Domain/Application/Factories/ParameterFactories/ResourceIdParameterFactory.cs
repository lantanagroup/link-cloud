using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;

public class ResourceIdParameterFactory : IResourceIdParameterFactory
{
    private readonly ILogger<ResourceIdParameterFactory> _logger;

    public ResourceIdParameterFactory(ILogger<ResourceIdParameterFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task<ParameterFactoryResult?> Build(ResourceIdsParameter parameter, GetPatientDataRequest request, IDataAcquisitionLogQueries dataAcquisitionLogQueries)
    {
        var reportTrackingId = request.ConsumeResult?.Message?.Value?.ScheduledReports
            ?.Select(report => report.ReportTrackingId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        List<string> resourceIds = await
            dataAcquisitionLogQueries.GetResourceIdsForReportPatient(
                request.CorrelationId,
                request.FacilityId,
                reportTrackingId,
                parameter.Resource);

        if (resourceIds == null || !resourceIds.Any())
        {
            _logger.LogWarning("ResourceIdsParameter validation failed: resourceIds is null or empty. Parameter Name: {Name}", parameter.Name);
            return null;
        }

        Int32.TryParse(parameter.Paged, out int configuredPageSize);
        var pageSize = configuredPageSize > 0
            ? Math.Min(configuredPageSize, FhirSearchLimits.MaxIdsPerParameter)
            : FhirSearchLimits.MaxIdsPerParameter;

        if (resourceIds.Count > pageSize)
        {
            var pagedEntries = resourceIds.Chunk(pageSize).ToList();
            return new ParameterFactoryResult(parameter.Name, null, true, pagedEntries);
        }

        var joinedEntries = string.Join(",", resourceIds);
        if (string.IsNullOrWhiteSpace(joinedEntries))
            return null;

        return new ParameterFactoryResult(parameter.Name, joinedEntries);
    }
}
