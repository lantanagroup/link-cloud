using DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;

public class ReferenceQueryFactory
{
    public static ReferenceQueryFactoryResult Build(ReferenceQueryConfig config, List<ResourceReference> referenceResources)
    {
        return new ReferenceQueryFactoryResult(config.ResourceType, referenceResources);
    }
}
