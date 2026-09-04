using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Capabilities;

// Registered unconditionally, same reasoning as SftpFileFixtureGateway — nothing to branch to
// until the client confirms the intended response shape and the real adapter can be written
// against it. Patient count is randomized to a realistic list size (matching the onboarding POC)
// so the results preview isn't always a trivially small list; the shape stays PatientIds-only per
// IPatientListGateway's contract note, since the real list API has no per-patient name field to
// eventually populate here.
internal sealed class PatientListFixtureGateway : IPatientListGateway
{
    public Task<CensusListResult> QueryAsync(string facilityId, string listKey, CancellationToken cancellationToken = default)
    {
        var patientIds = Enumerable.Range(1, Random.Shared.Next(5, 51))
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
