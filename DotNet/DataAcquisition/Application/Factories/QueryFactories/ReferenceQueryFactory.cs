using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories.QueryFactories;

public class ReferenceQueryFactory
{
    public static ReferenceQueryFactoryResult Build(ReferenceQueryConfig config, List<ResourceReference> referenceResources)
    {
        return new ReferenceQueryFactoryResult(config.ResourceType, referenceResources);
    }
}
