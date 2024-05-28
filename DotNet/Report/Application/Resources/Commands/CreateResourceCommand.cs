using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.PatientResource.Commands;
using LantanaGroup.Link.Report.Application.SharedResource.Commands;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

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
            bool isPatientResourceType = PatientResourceProvider.GetPatientResourceTypes().Any(x => x == request.Resource.TypeName);

            if (isPatientResourceType)
            {
                return await _mediator.Send(new CreatePatientResourceCommand(request.FacilityId, request.PatientId, request.Resource));
                
            }

            return await _mediator.Send(new CreateSharedResourceCommand(request.FacilityId, request.Resource));
        }
    }
}
