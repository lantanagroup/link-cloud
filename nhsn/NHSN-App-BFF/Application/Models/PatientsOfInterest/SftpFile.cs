namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

// One file from Data Acquisition's sFTP test-connection call, patients already attached — the
// real endpoint returns files with their patients in the same response, no separate preview call.
// Identified by FileName; the endpoint has no file id. QueriedAt is stamped once per call, not
// read from the response, since the endpoint does not report it.
public class SftpFile
{
    public required string FileName { get; set; }
    public required DateTimeOffset QueriedAt { get; set; }
    public required IReadOnlyList<SftpFilePatient> Patients { get; set; }
    public bool Simulated { get; set; }
}

public class SftpFilePatient
{
    public required string PatientId { get; set; }
    public required string PatientName { get; set; }
}
