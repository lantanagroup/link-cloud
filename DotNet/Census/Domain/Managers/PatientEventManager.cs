using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Repositories;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface IPatientEventManager
{
    Task<List<PatientEventModel>> GetByFacilityIdAndPatientId(string facilityId, string patientId, CancellationToken cancellationToken);
    Task<PatientEventModel> AddPatientEvent(PatientEventModel patientEvent, CancellationToken cancellationToken);
    Task DeletePatientEventById(Guid id, CancellationToken cancellationToken);
}
public class PatientEventManager : IPatientEventManager
{
    private readonly IDatabase _database;

    public PatientEventManager(IDatabase database)
    {
        _database = database;
    }

    public async Task<List<PatientEventModel>> GetByFacilityIdAndPatientId(string facilityId, string patientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID cannot be null or empty.", nameof(patientId));
        }

        var results = await _database.PatientEventRepository.FindAsync(x => x.FacilityId == facilityId && x.SourcePatientId == patientId, cancellationToken);

        return results.Select(PatientEventModel.FromDomain).ToList();
    }

    public async Task<PatientEventModel> AddPatientEvent(PatientEventModel model, CancellationToken cancellationToken)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "Patient event cannot be null.");
        }

        var entity = new PatientEvent
        {
            FacilityId = model.FacilityId,
            CorrelationId = model.CorrelationId,
            EventType = model.EventType,
            MedicalRecordNumber = model.MedicalRecordNumber,
            Payload = model.Payload,
            SourcePatientId = model.SourcePatientId,
            SourceVisitId = model.SourceVisitId,
            SourceType = model.SourceType,
            CreateDate = DateTime.UtcNow,
        };

        var result = await _database.PatientEventRepository.AddAsync(entity, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);

        return PatientEventModel.FromDomain(entity);
    }

    public async Task DeletePatientEventById(Guid id, CancellationToken cancellationToken)
    {
        if (id == default)
        {
            throw new ArgumentException("Patient event ID cannot be null or empty.", nameof(id));
        }

        var entity = await _database.PatientEventRepository.GetAsync(id);

        _database.PatientEventRepository.Remove(entity);
        await _database.SaveChangesAsync(cancellationToken);
    }
}
