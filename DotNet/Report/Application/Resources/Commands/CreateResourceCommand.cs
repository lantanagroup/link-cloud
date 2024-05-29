using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.PatientResource.Commands;
using LantanaGroup.Link.Report.Application.SharedResource.Commands;
using LantanaGroup.Link.Report.Application.ResourceCategories;
using MediatR;
using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Application.Resources.Commands
{
    public class CreateResourceCommand : IRequest<IFacilityResource>
    {
        public string FacilityId { get; private set; }
        public string PatientId { get; private set; }
        public Resource Resource { get; private set; }

        public CreateResourceCommand(string facilityId, string patientId, Resource resource) 
        { 
            FacilityId = facilityId;
            PatientId = patientId;
            Resource = resource;
        }
    }

    public class CreateResourceHandler : IRequestHandler<CreateResourceCommand, IFacilityResource>
    {
        private readonly IMediator _mediator;

        public CreateResourceHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<IFacilityResource> Handle(CreateResourceCommand request, CancellationToken cancellationToken)
        {
            var resourceTypeCategory = ResourceCategory.GetResourceCategoryByType(request.Resource.TypeName);

            if (resourceTypeCategory == null)
            {
                throw new Exception(request.Resource.TypeName + " is not a valid FHIR resouce");
            }

            if (resourceTypeCategory == ResourceCategoryType.Patient)
            {
                return await _mediator.Send(new CreatePatientResourceCommand(request.FacilityId, request.PatientId, request.Resource));
                
            }

            return await _mediator.Send(new CreateSharedResourceCommand(request.FacilityId, request.Resource));
        }
    }
}
