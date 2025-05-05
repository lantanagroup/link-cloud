using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface ICensusPatientListManager
{
    Task<CensusPatientListEntity> AddAsync(CensusPatientListEntity entity,
        CancellationToken cancellationToken = default);

    Task<CensusPatientListEntity> UpdateAsync(CensusPatientListEntity entity,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<CensusPatientListEntity>> GetPatientList(string facilityId, DateTime? startDate, DateTime? endDate);
    
    Task<List<CensusPatientListEntity>> GetPatientListForFacility(string facilityId, bool activeOnly, CancellationToken cancellationToken = default);

    Task<CensusPatientListEntity> GetPatientByPatientId(string facilityId, string patientId,
        CancellationToken cancellationToken = default);

    Task<CensusPatientListEntity> AddOrUpdateAsync(CensusPatientListEntity entity,
        CancellationToken cancellationToken = default);
    
}

public class CensusPatientListManager : ICensusPatientListManager
{
    private readonly ILogger<CensusPatientListManager> _logger;
    private readonly IEntityRepository<CensusPatientListEntity> _patientListRepository;

    public CensusPatientListManager(ILogger<CensusPatientListManager> logger, IEntityRepository<CensusPatientListEntity> patientListRepository)
    {
        _logger = logger;
        _patientListRepository = patientListRepository;
    }

    public async Task<CensusPatientListEntity> AddOrUpdateAsync(CensusPatientListEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id != null)
        {
            return await UpdateAsync(entity, cancellationToken);
        }
        else
        {
            return await AddAsync(entity, cancellationToken);
        }
    }

    public async Task<CensusPatientListEntity> AddAsync(CensusPatientListEntity entity, CancellationToken cancellationToken = default)
    {
        return await _patientListRepository.AddAsync(entity, cancellationToken);
    }
    public async Task<CensusPatientListEntity> UpdateAsync(CensusPatientListEntity entity, CancellationToken cancellationToken = default)
    {
        entity.ModifyDate = DateTime.UtcNow;

        return await _patientListRepository.UpdateAsync(entity, cancellationToken);
    }
    public async Task<IEnumerable<CensusPatientListEntity>> GetPatientList(string facilityId, DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && !endDate.HasValue && startDate.Value != default && endDate.Value == default)
        {
            return (await _patientListRepository.FindAsync(c => c.FacilityId == facilityId && (c.AdmitDate >= startDate && c.AdmitDate <= endDate))).DistinctBy(p => p.PatientId).ToList();
        }
        else if (!startDate.HasValue && endDate.HasValue && startDate.Value == default && endDate.Value != default)
        {
            return (await _patientListRepository.FindAsync(c => c.FacilityId == facilityId && (c.DischargeDate <= endDate && c.AdmitDate <= endDate))).DistinctBy(p => p.PatientId).ToList();
        }
        else if (startDate.HasValue && endDate.HasValue && startDate.Value != default && endDate.Value != default)
        {
            return (await _patientListRepository.FindAsync(c => c.FacilityId == facilityId && c.AdmitDate <= endDate && (c.DischargeDate == default || c.DischargeDate >= startDate))).DistinctBy(p => p.PatientId).ToList();
        }
        else
        {
            return (await _patientListRepository.FindAsync(c => c.FacilityId == facilityId)).DistinctBy(p => p.PatientId).ToList();
        }
    }

    public async Task<List<CensusPatientListEntity>> GetPatientListForFacility(string facilityId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var activePatients = await _patientListRepository.FindAsync(x => x.FacilityId == facilityId && (!activeOnly || (activeOnly && !x.IsDischarged)), cancellationToken);
        return activePatients;
    }

    public async Task<CensusPatientListEntity> GetPatientByPatientId(string facilityId, string patientId, CancellationToken cancellationToken = default)
    {
        return await _patientListRepository.FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.PatientId == patientId, cancellationToken);
    }
}
