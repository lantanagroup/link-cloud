namespace LantanaGroup.Link.DataAcquisition.Models;

/// <summary>
/// Result model for SFTP connection test operations.
/// </summary>
public class SftpConnectionTestResult
{
    /// <summary>
    /// Indicates whether the connection test was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// A message describing the result of the connection test.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
