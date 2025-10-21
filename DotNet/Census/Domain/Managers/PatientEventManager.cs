using LantanaGroup.Link.Census.Domain.Entities.POI;
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
    private readonly IPatientEncounterManager _patientEncounterManager;

    public PatientEventManager(
        IBaseEntityRepository<PatientEvent> patientEventRepository,
        IPatientEncounterManager patientEncounterManager
        )
    {
        _patientEventRepository = patientEventRepository ?? throw new ArgumentNullException(nameof(patientEventRepository));
        _patientEncounterManager = patientEncounterManager ?? throw new ArgumentNullException(nameof(patientEncounterManager));
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

    public async Task DeletePatientEventById(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Patient event ID cannot be null or empty.", nameof(id));
        }

        var toBeDeletedEvent = await _patientEventRepository.GetAsync(id, cancellationToken);

        if (toBeDeletedEvent == null)
        {
            throw new Exception($"Patient event with ID {id} not found.");
        }

        await _patientEventRepository.DeleteAsync(id, cancellationToken);
    }
}
