using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Repositories;
using MediatR;

namespace LantanaGroup.Link.Census.Commands;

public class GetCensusHistoryQuery : IRequest<List<PatientCensusHistoricEntity>>
{
    public string FacilityId { get; set; }
}

public class GetCensusHistoryQueryHandler : IRequestHandler<GetCensusHistoryQuery, List<PatientCensusHistoricEntity>>
{
    private readonly ILogger<GetCensusHistoryQueryHandler> _logger;
    private readonly ICensusHistoryRepository _repository;

    public GetCensusHistoryQueryHandler(ILogger<GetCensusHistoryQueryHandler> logger, ICensusHistoryRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<PatientCensusHistoricEntity>> Handle(GetCensusHistoryQuery request, CancellationToken cancellationToken)
    {
        var histories = _repository.GetAllCensusReportsForFacility(request.FacilityId);
        return histories;
    }
}
