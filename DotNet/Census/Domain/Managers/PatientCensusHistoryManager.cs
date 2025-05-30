using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Quartz;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface IPatientCensusHistoryManager
{
    Task<PatientCensusHistoricEntity> AddAsync(PatientCensusHistoricEntity entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<PatientCensusHistoricEntity>> GetPatientCensusHistoryByFacilityId(string facilityId);
}

public class PatientCensusHistoryManager : IPatientCensusHistoryManager
{
    private readonly ILogger<PatientCensusHistoryManager> _logger;
    private readonly IBaseEntityRepository<PatientCensusHistoricEntity> _censusHistoryRepository;
    private readonly ITenantApiService _tenantApiService;

    public PatientCensusHistoryManager(ILogger<PatientCensusHistoryManager> logger,
        IBaseEntityRepository<PatientCensusHistoricEntity> censusHistoryRepository, ITenantApiService tenantApiService)
    {
        _logger = logger;
        _censusHistoryRepository = censusHistoryRepository;
        _tenantApiService = tenantApiService;
    }

    public async Task<PatientCensusHistoricEntity> AddAsync(PatientCensusHistoricEntity entity, CancellationToken cancellationToken = default)
    {
        return await _censusHistoryRepository.AddAsync(entity, cancellationToken);
    }

    public async Task<IEnumerable<PatientCensusHistoricEntity>> GetPatientCensusHistoryByFacilityId(string facilityId)
    {
        return await _censusHistoryRepository.FindAsync(c => c.FacilityId == facilityId);
    }
}
