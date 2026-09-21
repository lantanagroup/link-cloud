namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

/// <summary>
/// Represents a file retrieved from the SFTP server during a test connection.
/// </summary>
public record SftpTestFileModel
{
    /// <summary>
    /// Name of the file retrieved from the SFTP server.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Patients in the file retrieved from the SFTP server.
    /// </summary>
    public SftpTestFilePatientModel[] Patients { get; set; } = Array.Empty<SftpTestFilePatientModel>();
}