using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface IPatientEncounterManager
{
    public Task<PatientEncounter> AddPatientEncounterAsync(PatientEncounter patientEncounter, CancellationToken cancellationToken);
    public Task<PatientEncounter> UpdatePatientEncounterAsync(PatientEncounter patientEncounter, CancellationToken cancellationToken);
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
        return _patientEncounterRepository.AddAsync(patientEncounter, cancellationToken);
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
