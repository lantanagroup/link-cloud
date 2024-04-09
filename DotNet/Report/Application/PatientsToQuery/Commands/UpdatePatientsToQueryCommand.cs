using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;

// This will be unused for the time being. We are instead going to update directly the MeasureReportSchedule.PatientsToQuery field.
namespace LantanaGroup.Link.Report.Application.PatientsToQuery.Commands
{
    public class UpdatePatientsToQueryCommand : IRequest<PatientsToQueryModel>
    {
        public PatientsToQueryModel PatientsToQuery { get; set; } = default!;
    }

    public class UpdatePatientsToQueryCommandHandler : IRequestHandler<UpdatePatientsToQueryCommand, PatientsToQueryModel>
    {
        private readonly ILogger<UpdatePatientsToQueryCommandHandler> _logger;
        private readonly PatientsToQueryRepository _repository;

        public UpdatePatientsToQueryCommandHandler(ILogger<UpdatePatientsToQueryCommandHandler> logger, PatientsToQueryRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<PatientsToQueryModel> Handle(UpdatePatientsToQueryCommand request, CancellationToken cancellationToken)
        {
            var patientsToQuery = await _repository.GetAsync(request.PatientsToQuery.Id);
            if (patientsToQuery == null)
            {
                throw new Exception($"No report schedule found for {request.PatientsToQuery.Id}");
            }

            patientsToQuery.FacilityId = request.PatientsToQuery.FacilityId;
            patientsToQuery.PatientIds = request.PatientsToQuery.PatientIds;
            patientsToQuery.ModifyDate = DateTime.UtcNow;
            await _repository.UpdateAsync(patientsToQuery);
            return patientsToQuery;
        }
    }
}
