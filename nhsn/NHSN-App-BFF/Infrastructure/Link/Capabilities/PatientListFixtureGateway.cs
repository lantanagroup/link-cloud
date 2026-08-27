using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Capabilities;

// Registered unconditionally, same reasoning as SftpFileFixtureGateway — nothing to branch to
// until Q-21 closes and the real adapter can be written against a settled shape.
internal sealed class PatientListFixtureGateway : IPatientListGateway
{
    public Task<CensusListResult> QueryAsync(string facilityId, string listKey, CancellationToken cancellationToken = default)
    {
        var patientIds = Enumerable.Range(1, 3)
            .Select(i => $"SIMULATED-PATIENT-{i:D4}")
            .ToArray();

        return Task.FromResult(new CensusListResult
        {
            ListKey = listKey,
            PatientCount = patientIds.Length,
            PatientIds = patientIds,
            Simulated = true
        });
    }
}
