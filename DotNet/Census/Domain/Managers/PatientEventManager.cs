using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface IPatientEventManager
{
    Task<List<PatientEvent>> GetByFacilityIdAndPatientId(string facilityId, string patientId, CancellationToken cancellationToken);
    Task<PatientEvent> AddPatientEvent(PatientEvent patientEvent, CancellationToken cancellationToken);
    Task DeletePatientEventById(string id, CancellationToken cancellationToken);
}
public class PatientEventManager : IPatientEventManager
{
    private readonly IBaseEntityRepository<PatientEvent> _patientEventRepository;

    public PatientEventManager(IBaseEntityRepository<PatientEvent> patientEventRepository)
    {
        _patientEventRepository = patientEventRepository ?? throw new ArgumentNullException(nameof(patientEventRepository));
    }

    public async Task<List<PatientEvent>> GetByFacilityIdAndPatientId(string facilityId, string patientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID cannot be null or empty.", nameof(patientId));
        }

        return await _patientEventRepository.FindAsync(x => x.FacilityId == facilityId && x.SourcePatientId == patientId, cancellationToken);
    }

    public async Task<PatientEvent> AddPatientEvent(PatientEvent patientEvent, CancellationToken cancellationToken)
    {
        if (patientEvent == null)
        {
            throw new ArgumentNullException(nameof(patientEvent), "Patient event cannot be null.");
        }

        patientEvent.CreateDate = DateTime.UtcNow;
        patientEvent.ModifyDate = DateTime.UtcNow;

        return await _patientEventRepository.AddAsync(patientEvent, cancellationToken);
    }

    public Task DeletePatientEventById(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Patient event ID cannot be null or empty.", nameof(id));
        }
        return _patientEventRepository.DeleteAsync(id, cancellationToken);
    }
}
