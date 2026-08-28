using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Epic's patient-list census query, one of the vendor's six list keys.
//
// Fixture-only: the spec adds firstName/lastName as scalars on a six-row configuration model, one
// name per list, not the patient collection the screen needs. The client needs to confirm the
// intended shape; this port and its caller do not change once that lands, only the adapter does.
public interface IPatientListGateway
{
    Task<CensusListResult> QueryAsync(string facilityId, string listKey, CancellationToken cancellationToken = default);
}
