namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Represents an active SFTP connection session for performing file operations.
/// All operations use the same underlying connection for efficiency.
/// Implements <see cref="IAsyncDisposable"/> to ensure proper cleanup of the connection.
/// </summary>
/// <remarks>
/// Sessions should be disposed when no longer needed to release the connection.
/// Use <c>await using</c> for automatic disposal.
/// </remarks>
public interface ISftpSession : IAsyncDisposable
{
    /// <summary>
    /// Lists files in the specified remote directory that match the given pattern.
    /// </summary>
    /// <param name="remoteDirectory">The absolute path to the remote directory to list.</param>
    /// <param name="fileNamePattern">
    /// Optional glob pattern to filter files (e.g., "*.csv", "census_*.txt").
    /// If <c>null</c> or empty, all files are returned.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of <see cref="SftpFileInfo"/> objects for matching files, ordered by last write time.</returns>
    Task<List<SftpFileInfo>> ListFilesAsync(
        string remoteDirectory,
        string? fileNamePattern,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a file from the remote server and returns its contents as a stream.
    /// </summary>
    /// <param name="remoteFilePath">The absolute path to the remote file to download.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A <see cref="MemoryStream"/> containing the file contents, positioned at the beginning.</returns>
    /// <remarks>The caller is responsible for disposing the returned stream.</remarks>
    Task<MemoryStream> DownloadFileAsync(
        string remoteFilePath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves a file to a different directory on the remote server.
    /// Creates the destination directory if it does not exist.
    /// </summary>
    /// <param name="sourceFilePath">The absolute path to the file to move.</param>
    /// <param name="destinationDirectory">The absolute path to the destination directory.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MoveFileAsync(
        string sourceFilePath,
        string destinationDirectory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a file from the remote server.
    /// </summary>
    /// <param name="remoteFilePath">The absolute path to the file to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteFileAsync(
        string remoteFilePath,
        CancellationToken cancellationToken);
}

/// <summary>
/// Contains metadata about a remote SFTP file.
/// </summary>
public class SftpFileInfo
{
    /// <summary>
    /// Gets or sets the file name without the directory path.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full absolute path to the file on the remote server.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last modification time of the file.
    /// </summary>
    public DateTime LastWriteTime { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long Length { get; set; }
}
