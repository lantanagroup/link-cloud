using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.PatientResource.Commands
{
    public class UpdatePatientResourceCommand : IRequest<PatientResourceModel>
    {
        public string FacilityId { get; private set; }
        public string PatientId { get; private set; }
        public Resource Resource { get; private set; }

        public UpdatePatientResourceCommand(string facilityId, string patientId, Resource resource) { 
            FacilityId = facilityId;
            PatientId = patientId;
            Resource = resource;
        }
    }

    public class UpdatePatientResourceCommandHandler : IRequestHandler<UpdatePatientResourceCommand, PatientResourceModel>
    {
        private readonly PatientResourceRepository _repository;
        private readonly IMediator _mediator;

        public UpdatePatientResourceCommandHandler(PatientResourceRepository repository, IMediator mediator) 
        { 
            _repository = repository;
            _mediator = mediator;
        }

        public async Task<PatientResourceModel> Handle(UpdatePatientResourceCommand request, CancellationToken cancellationToken)
        {
            


            var patientResource = new PatientResourceModel()
            {
                CreateDate = DateTime.UtcNow,
                FacilityId = request.FacilityId,
                PatientId = request.PatientId,
                Resource = JsonSerializer.Serialize<Resource>(request.Resource, new JsonSerializerOptions().ForFhir()),
                ResourceType = request.Resource.TypeName,
                ResourceId = request.Resource.Id
            };
            
            await _repository.AddAsync(patientResource);

            return patientResource;
        }
    }
}
