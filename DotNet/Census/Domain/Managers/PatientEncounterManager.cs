using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface IPatientEncounterManager
{
    public Task<PatientEncounter> AddPatientEncounterAsync(PatientEncounter patientEncounter, CancellationToken cancellationToken);
    public Task<PatientEncounter> UpdatePatientEncounterAsync(PatientEncounter patientEncounter, CancellationToken cancellationToken);
    public Task<IEnumerable<PatientEncounterModel>> GetPatientEncounterModels(string facilityId, string correlationId, CancellationToken cancellationToken = default);
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

    public Task<PatientEncounter> AddPatientEncounterAsync(PatientEncounter patientEncounter, CancellationToken cancellationToken)
    {
        if (patientEncounter == null)
        {
            throw new ArgumentNullException(nameof(patientEncounter));
        }

        if( string.IsNullOrEmpty(patientEncounter.FacilityId))
        {
            throw new ArgumentException("FacilityId cannot be null or empty.", nameof(patientEncounter.FacilityId));
        }

        if ( string.IsNullOrEmpty(patientEncounter.Id))
        {
            patientEncounter.Id = Guid.NewGuid().ToString();
        }

        patientEncounter.CreateDate = DateTime.UtcNow;
        patientEncounter.ModifyDate = patientEncounter.CreateDate;

        //loop through patient visit identifiers and patient identifiers and ensure that an id is assigned and create date
        foreach (var visitIdentifier in patientEncounter.PatientIdentifiers)
        {
            if (string.IsNullOrEmpty(visitIdentifier.Id))
            {
                visitIdentifier.Id = Guid.NewGuid().ToString();
            }
            if (visitIdentifier.CreateDate == default)
            {
                visitIdentifier.CreateDate = DateTime.UtcNow;
            }
        }

        foreach (var patientIdentifier in patientEncounter.PatientVisitIdentifiers)
        {
            if (string.IsNullOrEmpty(patientIdentifier.Id))
            {
                patientIdentifier.Id = Guid.NewGuid().ToString();
            }
            if (patientIdentifier.CreateDate == default)
            {
                patientIdentifier.CreateDate = DateTime.UtcNow;
            }
        }

        return _patientEncounterRepository.AddAsync(patientEncounter, cancellationToken);
    }

    public async Task<IEnumerable<PatientEncounterModel>> GetPatientEncounterModels(string facilityId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }

        bool isCorrelationIdEmpty = string.IsNullOrEmpty(correlationId);

        var encounters = await _patientEncounterRepository.FindAsync(
                x => x.FacilityId == facilityId && (isCorrelationIdEmpty || x.CorrelationId == correlationId),
                cancellationToken);

        var models = encounters
            .Select(PatientEncounterModel.FromDomain);

        return models;
    }

    

    public Task<PatientEncounter> UpdatePatientEncounterAsync(PatientEncounter patientEncounter, CancellationToken cancellationToken)
    {
        if (patientEncounter == null)
        {
            throw new ArgumentNullException(nameof(patientEncounter));
        }

        return _patientEncounterRepository.UpdateAsync(patientEncounter, cancellationToken);
    }
}
