using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.QueryResource
{
    public class QuerySharedResource
    {
        private readonly ILogger<QuerySharedResource> _logger;
        private readonly IFhirApiRepository _fhirRepo;

        public QuerySharedResource(ILogger<QuerySharedResource> logger, IFhirApiRepository fhirRepo)
        {
            _logger = logger;
            _fhirRepo = fhirRepo;
        }

        public async Task<DomainResource> GetResource(FhirQueryConfiguration fhirQueryConfig, string resourceType, IQueryConfig query, string facilityId, QueryPlanType queryPlanType) 
        {
            //TODO: Daniel - check if bundle is needed
            QueryFactoryResult builtQuery = ReferenceQueryFactory.Build((ReferenceQueryConfig)query, new Hl7.Fhir.Model.Bundle());

            var resources = await _fhirRepo.GetReferenceResource(
                    fhirQueryConfig.FhirServerBaseUrl,
                    resourceType,
                    null,
                    facilityId,
                    null,
                    queryPlanType.ToString(),
                    new List<Hl7.Fhir.Model.ResourceReference>(),
                    (ReferenceQueryConfig)query,
                    fhirQueryConfig.Authentication);

            return resources.FirstOrDefault();
        }
    }
}
