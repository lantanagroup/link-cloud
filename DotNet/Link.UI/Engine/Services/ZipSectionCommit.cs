using System.IO.Compression;

namespace Automation.UI.Services;

/// <summary>
/// Copies <paramref name="targetPath"/> into a new zip, applies <paramref name="mutate"/>,
/// and replaces the original only after that zip is fully written.
/// Update mode is not used: opening an entry there stores the whole entry in a
/// MemoryStream until the archive is disposed. A failure deletes the new file
/// and leaves the original unchanged.
/// </summary>
public static class ZipSectionCommit
{
    public static async Task CommitAsync(
        string targetPath,
        Func<ZipArchive, CancellationToken, Task> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        ArgumentNullException.ThrowIfNull(mutate);

        var pendingPath = targetPath + ".pending";
        try
        {
            await using (var pending = new FileStream(
                pendingPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            using (var destination = new ZipArchive(pending, ZipArchiveMode.Create, leaveOpen: true))
            {
                await CopyEntriesAsync(targetPath, destination, cancellationToken).ConfigureAwait(false);
                await mutate(destination, cancellationToken).ConfigureAwait(false);
            }

            File.Move(pendingPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDelete(pendingPath);
            throw;
        }
    }

    private static async Task CopyEntriesAsync(string sourcePath, ZipArchive destination, CancellationToken cancellationToken)
    {
        await using var sourceFile = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous);
        using var source = new ZipArchive(sourceFile, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in source.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var created = destination.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            await using var input = entry.Open();
            await using var output = created.Open();
            await input.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
