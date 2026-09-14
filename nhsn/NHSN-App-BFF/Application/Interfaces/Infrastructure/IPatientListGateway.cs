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

    // Fixture-only: the spec adds firstName/lastName as scalars on a six-row configuration model, one
    // name per list, not the patient collection the screen needs. The client needs to confirm the
    // intended shape; this port and its caller do not change once that lands, only the adapter does.
    Task<CensusListResult> QueryAsync(string facilityId, string listKey, CancellationToken cancellationToken = default);
}
