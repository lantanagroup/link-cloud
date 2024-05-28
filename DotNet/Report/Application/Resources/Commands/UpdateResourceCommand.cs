using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.PatientResource.Commands;
using LantanaGroup.Link.Report.Application.SharedResource.Commands;
using LantanaGroup.Link.Report.Entities;
using MediatR;

namespace LantanaGroup.Link.Report.Application.Resources.Commands
{
    public class UpdateResourceCommand : IRequest<IFacilityResource>
    {
        public IFacilityResource ReportResource { get; private set; }
        public Resource UpdatedResource { get; private set; }

        public UpdateResourceCommand(IFacilityResource reportResource, Resource updatedResource) 
        { 
            ReportResource = reportResource;
            UpdatedResource = updatedResource;
        }
    }

    public class UpdateResourceCommandHandler : IRequestHandler<UpdateResourceCommand, IFacilityResource>
    {
        private readonly IMediator _mediator;

        public UpdateResourceCommandHandler(IMediator mediator)
        { 
            _mediator = mediator;
        }

        public async Task<IFacilityResource> Handle(UpdateResourceCommand request, CancellationToken cancellationToken)
        {
            if (request.GetType() == typeof(PatientResourceModel))
            {
                return await _mediator.Send(new UpdatePatientResourceCommand(request.ReportResource as PatientResourceModel, request.UpdatedResource));
            }
            
            return await _mediator.Send(new UpdateSharedResourceCommand(request.ReportResource as SharedResourceModel, request.UpdatedResource));
        }
    }
}
