namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

public record SftpTestConnectionResult
{
    /// <summary>
    /// Indicates whether the SFTP connection test was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// A message describing the result of the connection test.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Files retrieved from the SFTP server during the test connection.
    /// </summary>
    public SftpTestFileModel[]? Files { get; set; } = null;

}