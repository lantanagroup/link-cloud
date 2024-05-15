using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

namespace LantanaGroup.Link.Report.Application.PatientResource.Commands
{
    public class CreatePatientResourceCommand : IRequest<PatientResourceModel>
    {
        public PatientResourceModel PatientResource { get; set; } = default!;
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
            request.PatientResource.CreateDate = DateTime.UtcNow;

            await _repository.AddAsync(request.PatientResource);

            return request.PatientResource;
        }
    }
}
