using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories.ParameterFactories;

public class ResourceIdParameterFactory
{
    public static ParameterFactoryResult Build(ResourceIdsParameter parameter, GetPatientDataRequest request, List<string> resourceIds)
    {
        if (resourceIds == null || !resourceIds.Any())
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(parameter.Paged))
        {
            int pageSize = int.Parse(parameter.Paged);
            var pagedEntries = resourceIds.Chunk(pageSize).ToList();

            if (!pagedEntries.Any())
                return null;

            return new ParameterFactoryResult(parameter.Name, null, true, pagedEntries);
        }

        var joinedEntries = string.Join(",", resourceIds);
        if (string.IsNullOrWhiteSpace(joinedEntries))
            return null;

        return new ParameterFactoryResult(parameter.Name, joinedEntries);
    }
}
