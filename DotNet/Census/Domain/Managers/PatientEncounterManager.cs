// Modified PatientEncounterManager.cs
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface IPatientEncounterManager
{
    public Task<PatientEncounterModel> CreateAsync(CreatePatientEncounterModel model, CancellationToken cancellationToken = default);
    public Task<PatientEncounterModel> UpdateAsync(UpdatePatientEncounterModel model, CancellationToken cancellationToken = default);
    public Task DeleteAsync(string facilityId, string correlationId, CancellationToken cancellationToken = default);
}

public class PatientEncounterManager : IPatientEncounterManager
{
    private readonly ILogger<PatientEncounterManager> _logger;
    private readonly IBaseEntityRepository<PatientEncounter> _patientEncounterRepository;

    public PatientEncounterManager(ILogger<PatientEncounterManager> logger, IBaseEntityRepository<PatientEncounter> patientEncounterRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patientEncounterRepository = patientEncounterRepository ?? throw new ArgumentNullException(nameof(patientEncounterRepository));
    }

    public async Task<PatientEncounterModel> CreateAsync(CreatePatientEncounterModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (string.IsNullOrEmpty(model.FacilityId))
        {
            throw new ArgumentException("FacilityId cannot be null or empty.", nameof(model.FacilityId));
        }

        var entity = new PatientEncounter
        {
            Id = Guid.NewGuid(),
            CorrelationId = model.CorrelationId,
            FacilityId = model.FacilityId,
            MedicalRecordNumber = model.MedicalRecordNumber,
            AdmitDate = model.AdmitDate,
            DischargeDate = model.DischargeDate,
            EncounterType = model.EncounterType,
            EncounterStatus = model.EncounterStatus,
            EncounterClass = model.EncounterClass,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };

        entity.PatientIdentifiers = model.PatientIdentifiers.Select(pi => pi.ToDomain(entity.Id)).ToList();
        entity.PatientVisitIdentifiers = model.PatientVisitIdentifiers.Select(pvi => pvi.ToDomain(entity.Id)).ToList();

        await _patientEncounterRepository.AddAsync(entity, cancellationToken);

        return PatientEncounterModel.FromDomain(entity);
    }

    public async Task<PatientEncounterModel> UpdateAsync(UpdatePatientEncounterModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        var existingEntity = await _patientEncounterRepository.SingleOrDefaultAsync(
            x => x.FacilityId == model.FacilityId && x.CorrelationId == model.CorrelationId,
            cancellationToken);

        if (existingEntity == null)
        {
            throw new KeyNotFoundException($"PatientEncounter for FacilityId {model.FacilityId} and CorrelationId {model.CorrelationId} not found.");
        }

        existingEntity.MedicalRecordNumber = model.MedicalRecordNumber;
        existingEntity.AdmitDate = model.AdmitDate;
        existingEntity.DischargeDate = model.DischargeDate;
        existingEntity.EncounterType = model.EncounterType;
        existingEntity.EncounterStatus = model.EncounterStatus;
        existingEntity.EncounterClass = model.EncounterClass;
        existingEntity.ModifyDate = DateTime.UtcNow;

        existingEntity.PatientIdentifiers = model.PatientIdentifiers.Select(pi => pi.ToDomain(existingEntity.Id)).ToList();
        existingEntity.PatientVisitIdentifiers = model.PatientVisitIdentifiers.Select(pvi => pvi.ToDomain(existingEntity.Id)).ToList();

        await _patientEncounterRepository.UpdateAsync(existingEntity, cancellationToken);

        return PatientEncounterModel.FromDomain(existingEntity);
    }

    public async Task DeleteAsync(string facilityId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
        }

        var existing = await _patientEncounterRepository.SingleOrDefaultAsync(
            x => x.FacilityId == facilityId && x.CorrelationId == correlationId,
            cancellationToken);

        if (existing == null)
        {
            throw new KeyNotFoundException($"PatientEncounter for FacilityId {facilityId} and CorrelationId {correlationId} not found.");
        }

        await _patientEncounterRepository.RemoveAsync(existing, cancellationToken);
    }
}