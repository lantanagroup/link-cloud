using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using MediatR;
using MongoDB.Driver;

// This will be unused for the time being. We are instead going to update directly the MeasureReportSchedule.PatientsToQuery field.
namespace LantanaGroup.Link.Report.Application.PatientsToQuery.Queries
{
    public class FindPatientsToQueryQuery : IRequest<PatientsToQueryModel>
    {
        public string FacilityId { get; set; } = string.Empty;
    }

    public class FindPatientsToQueryQueryHandler : IRequestHandler<FindPatientsToQueryQuery, PatientsToQueryModel>
    {
        private readonly ILogger<FindPatientsToQueryQueryHandler> _logger;
        private readonly PatientsToQueryRepository _repository;

        public FindPatientsToQueryQueryHandler(ILogger<FindPatientsToQueryQueryHandler> logger, PatientsToQueryRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<PatientsToQueryModel> Handle(FindPatientsToQueryQuery request, CancellationToken cancellationToken)
        {
            var res = await _repository.FindAsync(x => x.FacilityId == request.FacilityId);
            return res.FirstOrDefault();
        }
    }
}