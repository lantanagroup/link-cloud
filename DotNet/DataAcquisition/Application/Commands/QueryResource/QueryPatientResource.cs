using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Commands.BulkData;
using LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;
using LantanaGroup.Link.DataAcquisition.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using Microsoft.EntityFrameworkCore.Update;
using System.Collections.Generic;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.QueryResource
{
    public class QueryPatientResource
    {
        private readonly ILogger<QuerySharedResource> _logger;
        private readonly IFhirApiRepository _fhirRepo;

        public QueryPatientResource(ILogger<QuerySharedResource> logger, IFhirApiRepository fhirRepo)
        {
            _logger = logger;
            _fhirRepo = fhirRepo;
        }
        
        //TODO: Daniel - Adding all the params needed for now.. Look into whether this can be limited down some.
        public async Task<List<Resource>> GetResource(FhirQueryConfiguration fhirQueryConfig, IQueryConfig query, GetPatientDataRequest request, ScheduledReport scheduledReport, string queryPlanLookback, QueryPlanType queryPlanType) 
        {
            //TODO: Daniel - Replace bundle dependency and return the queried resource
            Bundle bundle = new Bundle();

            var builtQuery = ParameterQueryFactory.Build((ParameterQueryConfig)query, request, scheduledReport, queryPlanLookback, bundle);

            if (builtQuery.GetType() == typeof(SingularParameterQueryFactoryResult))
            {
                bundle = await _fhirRepo.GetSingularBundledResultsAsync(
                    fhirQueryConfig.FhirServerBaseUrl,
                    request.Message.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType.ToString(),
                    bundle,
                    (SingularParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)query,
                    scheduledReport,
                    fhirQueryConfig.Authentication);
            }
            else if (builtQuery.GetType() == typeof(PagedParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)query;

                bundle = await _fhirRepo.GetPagedBundledResultsAsync(
                    fhirQueryConfig.FhirServerBaseUrl,
                    request.Message.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType.ToString(),
                    bundle,
                    (PagedParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)query,
                    scheduledReport,
                    fhirQueryConfig.Authentication);
            }

            List<Resource> resources = new List<Resource>();

            foreach (var entry in bundle.Entry) {
                resources.Add(entry.Resource);

                //TODO: Daniel - need to test
                foreach (var child in entry.Resource.Children.Where(x => x is Resource))
                {
                    resources.Add((Resource)child);
                }
            }

            return resources;
        }
    }
}
