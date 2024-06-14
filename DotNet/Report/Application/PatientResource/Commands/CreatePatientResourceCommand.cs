using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Application.PatientResource.Commands
{
    public class CreatePatientResourceCommand : IRequest<PatientResourceModel>
    {
        public string FacilityId { get; private set; }
        public string PatientId { get; private set; }
        public Resource Resource { get; private set; }

        public CreatePatientResourceCommand(string facilityId, string patientId, Resource resource) { 
            FacilityId = facilityId;
            PatientId = patientId;
            Resource = resource;
        }
    }

    public class CreatePatientResourceCommandHandler : IRequestHandler<CreatePatientResourceCommand, PatientResourceModel>
    {
        private readonly PatientResourceRepository _repository;

        public CreatePatientResourceCommandHandler(PatientResourceRepository repository) 
        { 
            _repository = repository;
        }

        public async Task<PatientResourceModel> Handle(CreatePatientResourceCommand request, CancellationToken cancellationToken)
        {
            var patientResource = new PatientResourceModel()
            {
                CreateDate = DateTime.UtcNow,
                FacilityId = request.FacilityId,
                PatientId = request.PatientId,
                Resource = request.Resource,
                ResourceType = request.Resource.TypeName,
                ResourceId = request.Resource.Id
            };
            
            await _repository.AddAsync(patientResource, cancellationToken);

            return patientResource;
        }
    }
}
