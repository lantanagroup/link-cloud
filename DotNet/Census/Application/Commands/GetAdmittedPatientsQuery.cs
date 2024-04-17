using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.Census.Application.Commands;

public class GetAdmittedPatientsQuery : IRequest<List<CensusPatientListEntity>>
{
    public string FacilityId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class GetAdmittedPatientsQueryHandler : IRequestHandler<GetAdmittedPatientsQuery, IEnumerable<CensusPatientListEntity>>
{
    private readonly ILogger<GetAdmittedPatientsQueryHandler> _logger;
    private readonly ICensusPatientListRepository _repository;

    public GetAdmittedPatientsQueryHandler(ILogger<GetAdmittedPatientsQueryHandler> logger, ICensusPatientListRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IEnumerable<CensusPatientListEntity>> Handle(GetAdmittedPatientsQuery request, CancellationToken cancellationToken)
    {
        List<CensusPatientListEntity> patients = null;
        if (request.StartDate != default && request.EndDate != default)
        {
            patients = await _repository.GetAllPatientsForFacility(request.FacilityId, request.StartDate, request.EndDate); 
        }
        else 
        {             
            patients = await _repository.GetActivePatientsForFacility(request.FacilityId);
        }
        return patients;
    }
}