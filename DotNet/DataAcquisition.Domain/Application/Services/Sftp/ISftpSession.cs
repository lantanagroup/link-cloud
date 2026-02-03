namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Represents an active SFTP connection session.
/// All operations use the same connection for efficiency.
/// Dispose when done to disconnect from the server.
/// </summary>
public interface ISftpSession : IAsyncDisposable
{
    /// <summary>
    /// Lists files in the remote directory matching the pattern.
    /// </summary>
    Task<List<SftpFileInfo>> ListFilesAsync(
        string remoteDirectory,
        string? fileNamePattern,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a file and returns its contents as a stream.
    /// Caller is responsible for disposing the returned stream.
    /// </summary>
    Task<MemoryStream> DownloadFileAsync(
        string remoteFilePath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves a file to a different directory.
    /// Creates the destination directory if it doesn't exist.
    /// </summary>
    Task MoveFileAsync(
        string sourceFilePath,
        string destinationDirectory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a file from the remote server.
    /// </summary>
    Task DeleteFileAsync(
        string remoteFilePath,
        CancellationToken cancellationToken);
}

/// <summary>
/// Information about a remote SFTP file.
/// </summary>
public class SftpFileInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime LastWriteTime { get; set; }
    public long Length { get; set; }
}
