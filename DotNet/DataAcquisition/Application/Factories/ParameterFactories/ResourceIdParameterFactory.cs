using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories.ParameterFactories;

public class ResourceIdParameterFactory
{
    public static ParameterFactoryResult Build(ResourceIdsParameter parameter, GetPatientDataRequest request, Hl7.Fhir.Model.Bundle bundle)
    {
        var entries = bundle.Entry.Where(x => x.TypeName == parameter.Resource).Select(x => x.Resource.Id).Distinct().ToList();
        if (string.IsNullOrWhiteSpace(parameter.Paged))
        {
            int pageSize = int.Parse(parameter.Paged);
            var pagedEntries = entries.Chunk(pageSize).ToList();

            if (!pagedEntries.Any())
                return null;

            return new ParameterFactoryResult(parameter.Name, null, true, pagedEntries);
        }

        var joinedEntries = string.Join(",", entries);
        if (string.IsNullOrWhiteSpace(joinedEntries))
            return null;

        return new ParameterFactoryResult(parameter.Name, joinedEntries);
    }
}
