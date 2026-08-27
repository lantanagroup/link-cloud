using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Capabilities;

// Registered unconditionally — there is no real adapter to branch to yet, since LinkSdk has no
// sFTP coverage. Obviously synthetic ids and names, so a lower-environment screenshot is never
// mistaken for a real facility's files.
internal sealed class SftpFileFixtureGateway : ISftpFileGateway
{
    public Task<IReadOnlyList<SftpFile>> ListFilesAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var queriedAt = DateTimeOffset.UtcNow;

        IReadOnlyList<SftpFile> files =
        [
            new SftpFile
            {
                FileName = "census-simulated-0001.csv",
                QueriedAt = queriedAt,
                Simulated = true,
                Patients =
                [
                    new SftpFilePatient { PatientId = "SIMULATED-PATIENT-0001", PatientName = "Jane Doe" }
                ]
            }
        ];

        return Task.FromResult(files);
    }
}
