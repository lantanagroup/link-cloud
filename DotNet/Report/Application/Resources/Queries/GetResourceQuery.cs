﻿using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.Resources.Queries
{
    public class GetResourceQuery : IRequest<IFacilityResource>
    {
        public string FacilityId { get; private set; }
        public string PatientId { get; private set; }
        public string ResourceType { get; private set; }
        public string ResourceId { get; private set; }

        public GetResourceQuery(string facilityId, string patientId, string resourceType, string resourceId)
        {
            FacilityId = facilityId;
            PatientId = patientId;
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }

    public class ResourceExistsCommandHandler : IRequestHandler<GetResourceQuery, IFacilityResource>
    {
        private readonly PatientResourceRepository _patientResourceRepository;
        private readonly SharedResourceRepository _sharedResourceRepository;

        public ResourceExistsCommandHandler(PatientResourceRepository patientResourceRepository, SharedResourceRepository sharedResourceRepository)
        {
            _patientResourceRepository = patientResourceRepository;
            _sharedResourceRepository = sharedResourceRepository;
        }

        public async Task<IFacilityResource> Handle(GetResourceQuery request, CancellationToken cancellationToken)
        {
            var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(request.ResourceType);

            if (resourceTypeCategory == null)
            {
                throw new Exception(request.ResourceType + " is not a valid FHIR resouce");
            }

            if (resourceTypeCategory == ResourceCategoryType.Patient)
            {
                var patientResource = await _patientResourceRepository.GetAsync(request.FacilityId, request.PatientId, request.ResourceId, request.ResourceType);

                return patientResource;
            }

            var sharedResource = await _sharedResourceRepository.GetAsync(request.FacilityId, request.ResourceId, request.ResourceType);

            return sharedResource;
        }
    }
}
