using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.Census.Application.Commands;

public class GetCensusHistoryQuery : IRequest<List<PatientCensusHistoricEntity>>
{
    public string FacilityId { get; set; }
}

public class GetCensusHistoryQueryHandler : IRequestHandler<GetCensusHistoryQuery, IEnumerable<PatientCensusHistoricEntity>>
{
    private readonly ILogger<GetCensusHistoryQueryHandler> _logger;
    private readonly ICensusHistoryRepository _repository;

    public GetCensusHistoryQueryHandler(ILogger<GetCensusHistoryQueryHandler> logger, ICensusHistoryRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IEnumerable<PatientCensusHistoricEntity>> Handle(GetCensusHistoryQuery request, CancellationToken cancellationToken)
    {
        var histories = await _repository.GetAllCensusReportsForFacility(request.FacilityId);
        return histories;
    }
}
