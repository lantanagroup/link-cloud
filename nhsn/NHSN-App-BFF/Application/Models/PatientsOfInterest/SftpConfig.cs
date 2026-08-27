namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

public class SftpConfig
{
    public required string Host { get; set; }
    public required int Port { get; set; }
    public required string RemoteDirectory { get; set; }
    public bool RemoveAfterProcessing { get; set; }
}
