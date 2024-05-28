using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.PatientResource.Queries
{
    public class GetPatientResourceCommand : IRequest<PatientResourceModel>
    {
        public string FacilityId { get; private set; }
        public string PatientId { get; private set; }
        public string ResourceType { get; private set; }
        public string ResourceId { get; private set; }

        public GetPatientResourceCommand(string facilityId, string patientId, string resourceType, string resourceId)
        {
            FacilityId = facilityId;
            PatientId = patientId;
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }

    public class GetPatientResourceCommandHandler : IRequestHandler<GetPatientResourceCommand, PatientResourceModel>
    {
        private readonly PatientResourceRepository _repository;

        public GetPatientResourceCommandHandler(PatientResourceRepository repository)
        {
            _repository = repository;
        }

        Task<PatientResourceModel> IRequestHandler<GetPatientResourceCommand, PatientResourceModel>.Handle(GetPatientResourceCommand request, CancellationToken cancellationToken)
        {
            return _repository.GetAsync(request.FacilityId, request.PatientId, request.ResourceId, request.ResourceType);
        }
    }
}
