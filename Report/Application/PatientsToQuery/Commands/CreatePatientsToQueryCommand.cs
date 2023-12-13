using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

// This will be unused for the time being. We are instead going to update directly the MeasureReportSchedule.PatientsToQuery field.
namespace LantanaGroup.Link.Report.Application.PatientsToQuery.Commands
{
    public class CreatePatientsToQueryCommand : IRequest<PatientsToQueryModel>
    {
        public PatientsToQueryModel PatientsToQuery { get; set; } = default!;
    }

    public class CreatePatientsToQueryCommandHandler : IRequestHandler<CreatePatientsToQueryCommand, PatientsToQueryModel>
    {
        private readonly ILogger<CreatePatientsToQueryCommandHandler> _logger;
        private readonly PatientsToQueryRepository _repository;

        public CreatePatientsToQueryCommandHandler(ILogger<CreatePatientsToQueryCommandHandler> logger, PatientsToQueryRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<PatientsToQueryModel> Handle(CreatePatientsToQueryCommand request, CancellationToken cancellationToken)
        {
            request.PatientsToQuery.CreateDate = DateTime.UtcNow;
            await _repository.AddAsync(request.PatientsToQuery);
            return request.PatientsToQuery;
        }
    }
}
