using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

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
            files.Count, remoteDirectory, fileNamePattern ?? "(all)");

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
            _logger.LogDebug("Created directory {Directory}", destinationDirectory);
        }

        // Build destination path (preserve file name)
        var fileName = Path.GetFileName(sourceFilePath);
        var destinationPath = $"{destinationDirectory.TrimEnd('/')}/{fileName}";

        // Move processed file to processed directory
        _client.RenameFile(sourceFilePath, destinationPath);
        _logger.LogDebug("Moved file {Source} to {Destination}", sourceFilePath, destinationPath);

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
