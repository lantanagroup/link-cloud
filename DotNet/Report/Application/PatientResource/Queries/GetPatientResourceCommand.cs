using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.PatientResource.Queries
{
    public class GetPatientResourceCommand : IRequest<PatientResourceModel>
    {
        public string Id { get; private set; }
        

        public GetPatientResourceCommand(string id)
        {
            this.Id = id;
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
            return _repository.GetAsync(request.Id);
        }
    }
}
