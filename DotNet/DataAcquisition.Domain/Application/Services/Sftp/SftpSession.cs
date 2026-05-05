using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.Text.RegularExpressions;

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
/// SFTP session that maintains a single connection for all operations.
/// </summary>
public class SftpSession : ISftpSession
{
    private readonly SftpClient _client;
    private readonly ILogger _logger;
    private bool _disposed;

    public SftpSession(SftpClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<List<SftpFileInfo>> ListFilesAsync(
        string remoteDirectory,
        string? fileNamePattern,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Validate the remote directory path before attempting SFTP operations
        if (string.IsNullOrWhiteSpace(remoteDirectory))
        {
            throw new InvalidOperationException(
                "Remote directory path is empty. Please configure a valid remote directory path (e.g., '/' for root).");
        }

        // Verify the remote directory exists before attempting to list
        if (!_client.Exists(remoteDirectory))
        {
            throw new InvalidOperationException(
                $"Remote directory '{remoteDirectory}' does not exist. Please verify the SFTP configuration.");
        }

        var files = _client.ListDirectory(remoteDirectory)
            .Where(f => !f.IsDirectory && MatchesPattern(f.Name, fileNamePattern))
            .OrderBy(f => f.LastWriteTime)
            .Select(f => new SftpFileInfo
            {
                Name = f.Name,
                FullName = f.FullName,
                LastWriteTime = f.LastWriteTime,
                Length = f.Length
            })
            .ToList();

        _logger.LogDebug(
            "Listed {FileCount} files in {Directory} matching pattern {Pattern}",
            files.Count.SanitizeForLog(), remoteDirectory.SanitizeForLog(), fileNamePattern.SanitizeForLog() ?? "(all)");

        return Task.FromResult(files);
    }

    /// <inheritdoc/>
    public Task<MemoryStream> DownloadFileAsync(
        string remoteFilePath,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var memoryStream = new MemoryStream();
        _client.DownloadFile(remoteFilePath, memoryStream);
        memoryStream.Position = 0;

        _logger.LogDebug("Downloaded file {FilePath} ({Bytes} bytes)", remoteFilePath, memoryStream.Length);

        return Task.FromResult(memoryStream);
    }

    /// <inheritdoc/>
    public Task MoveFileAsync(
        string sourceFilePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Ensure destination directory exists
        if (!_client.Exists(destinationDirectory))
        {
            _client.CreateDirectory(destinationDirectory);
            _logger.LogDebug("Created directory {Directory}", destinationDirectory.SanitizeForLog());
        }

        // Build destination path (preserve file name)
        var fileName = Path.GetFileName(sourceFilePath);
        var destinationPath = $"{destinationDirectory.TrimEnd('/')}/{fileName}";

        // Move processed file to processed directory
        _client.RenameFile(sourceFilePath, destinationPath);
        _logger.LogDebug("Moved file {Source} to {Destination}", sourceFilePath.SanitizeForLog(), destinationPath.SanitizeForLog());

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteFileAsync(
        string remoteFilePath,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _client.DeleteFile(remoteFilePath);
        _logger.LogDebug("Deleted file {FilePath}", remoteFilePath);

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        _disposed = true;

        if (_client.IsConnected)
        {
            _client.Disconnect();
            _logger.LogDebug("SFTP session disconnected");
        }

        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private static bool MatchesPattern(string fileName, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase);
    }
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