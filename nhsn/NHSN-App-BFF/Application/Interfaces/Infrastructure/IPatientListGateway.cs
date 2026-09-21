using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Epic's patient-list census configuration (the six FHIR List ids) and the per-list query used by
// the census step's "Validate" preview.
public interface IPatientListGateway
{
    // Reads the facility's six FHIR List ids, keyed by the frontend's CensusListKey strings
    // (admit-lt-24, admit-24-to-48, ...). Empty when no configuration exists yet.
    Task<IReadOnlyDictionary<string, string>> GetConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);

    // Creates or updates the facility's FHIR List configuration from all six ids. Also deletes a
    // stale sFTP configuration for the same facility first, if one exists — Data Acquisition allows
    // only one census acquisition method per facility, so a facility switching from Cerner to Epic
    // would otherwise fail with EntityAlreadyExistsException.
    Task SaveConfigurationAsync(string facilityId, IReadOnlyDictionary<string, string> patientListIds, CancellationToken cancellationToken = default);

    // Deletes the facility's FHIR List configuration if one exists. Called before saving a Cerner
    // sFTP configuration, for the same one-census-method-per-facility reason as above.
    Task DeleteConfigurationIfExistsAsync(string facilityId, CancellationToken cancellationToken = default);

    // The patients currently on one of the six lists, read live from the facility's EHR through Data
    // Acquisition. Empty when the key is unknown or that list is not configured.
    Task<CensusListResult> QueryAsync(string facilityId, string listKey, CancellationToken cancellationToken = default);
}
