using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Repositories;
using MediatR;

namespace LantanaGroup.Link.Census.Commands;

public class GetCurrentCensusQuery : IRequest<List<CensusPatientListEntity>>
{
    public string FacilityId { get; set; }
}

public class GetCurrentCensusQueryHandler : IRequestHandler<GetCurrentCensusQuery, List<CensusPatientListEntity>>
{
    private readonly ILogger<GetCurrentCensusQueryHandler> _logger;
    private readonly ICensusPatientListRepository _patientListRepository;

    public GetCurrentCensusQueryHandler(ILogger<GetCurrentCensusQueryHandler> logger, ICensusPatientListRepository patientListRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientListRepository = patientListRepository ?? throw new ArgumentNullException(nameof(patientListRepository));
    }

    public async Task<List<CensusPatientListEntity>> Handle(GetCurrentCensusQuery request, CancellationToken cancellationToken)
    {
        var patients = await _patientListRepository.GetActivePatientsForFacility(request.FacilityId);
        return patients;
    }
}
