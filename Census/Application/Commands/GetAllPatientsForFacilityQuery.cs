using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Repositories;
using MediatR;

namespace LantanaGroup.Link.Census.Application.Commands;

public class GetAllPatientsForFacilityQuery : IRequest<List<CensusPatientListEntity>>
{
    public string FacilityId { get; set; }
}

public class GetAllPatientsForFacilityQueryHandler : IRequestHandler<GetAllPatientsForFacilityQuery, List<CensusPatientListEntity>>
{
    private readonly ILogger<GetAllPatientsForFacilityQueryHandler> _logger;
    private readonly ICensusPatientListRepository _patientListRepository;

    public GetAllPatientsForFacilityQueryHandler(ILogger<GetAllPatientsForFacilityQueryHandler> logger, ICensusPatientListRepository patientListRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientListRepository = patientListRepository ?? throw new ArgumentNullException(nameof(patientListRepository));
    }

    public async Task<List<CensusPatientListEntity>> Handle(GetAllPatientsForFacilityQuery request, CancellationToken cancellationToken)
    {
        var patients = await _patientListRepository.GetAllPatientsForFacility(request.FacilityId);
        return patients;
    }
}

