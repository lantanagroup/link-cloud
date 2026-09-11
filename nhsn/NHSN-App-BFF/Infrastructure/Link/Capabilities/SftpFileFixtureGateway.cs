using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Capabilities;

// Registered unconditionally — there is no real adapter to branch to yet, since LinkSdk has no
// sFTP coverage. Obviously synthetic ids, so a lower-environment screenshot is never mistaken for
// a real facility's files. Multiple files with multiple patients each (volumes modeled on the
// onboarding POC) so the results preview and export exercise the same multi-row rendering a live
// facility will produce, rather than always showing a single row.
internal sealed class SftpFileFixtureGateway : ISftpFileGateway
{
    public Task<IReadOnlyList<SftpFile>> TestConnectionAsync(string facilityId, SftpConfig config, CancellationToken cancellationToken = default)
    {
        var queriedAt = DateTimeOffset.UtcNow;
        var random = Random.Shared;
        var patientSeq = 0;
        var files = new List<SftpFile>();

        foreach (var fileIndex in Enumerable.Range(1, random.Next(2, 6)))
        {
            var patientCount = random.Next(3, 16);
            var patientIds = new List<string>(patientCount);
            for (var i = 0; i < patientCount; i++)
            {
                patientSeq++;
                patientIds.Add($"SIMULATED-PATIENT-{patientSeq:D4}");
            }

            files.Add(new SftpFile
            {
                FileName = $"census_extract_{fileIndex}_{queriedAt:yyyy-MM-dd}.csv",
                QueriedAt = queriedAt,
                Simulated = true,
                PatientIds = patientIds
            });
        }

        return Task.FromResult<IReadOnlyList<SftpFile>>(files);
    }
}
