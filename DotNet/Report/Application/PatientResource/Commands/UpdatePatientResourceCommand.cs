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
        public PatientResourceModel PatientResource { get; private set; }
        public Resource UpdatedResource { get; private set; }

        public UpdatePatientResourceCommand(PatientResourceModel patientResource, Resource updatedResource) { 
            PatientResource = patientResource;
            UpdatedResource = updatedResource;
        }
    }

    public class UpdatePatientResourceCommandHandler : IRequestHandler<UpdatePatientResourceCommand, PatientResourceModel>
    {
        private readonly PatientResourceRepository _repository;

        public UpdatePatientResourceCommandHandler(PatientResourceRepository repository) 
        { 
            _repository = repository;
        }

        public async Task<PatientResourceModel> Handle(UpdatePatientResourceCommand request, CancellationToken cancellationToken)
        {
            request.PatientResource.ModifyDate = DateTime.UtcNow;
            request.PatientResource.Resource = request.UpdatedResource; //JsonSerializer.Serialize(request.UpdatedResource, new JsonSerializerOptions().ForFhir());
            
            var patientResource = await _repository.UpdateAsync(request.PatientResource);

            return patientResource;
        }
    }
}
