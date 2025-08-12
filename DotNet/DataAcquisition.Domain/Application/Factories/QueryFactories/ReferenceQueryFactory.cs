using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.QueryFactories;

public class ReferenceQueryFactory
{
    public static ReferenceQueryFactoryResult Build(ReferenceQueryConfig config, List<ResourceReference> referenceResources)
    {
        return new ReferenceQueryFactoryResult(config.ResourceType, referenceResources);
    }
}
