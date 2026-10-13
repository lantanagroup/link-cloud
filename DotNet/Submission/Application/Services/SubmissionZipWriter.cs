using System.IO.Compression;

namespace LantanaGroup.Link.Submission.Application.Services;

/// <summary>
/// Writes a submission ZIP to a caller-supplied stream one entry at a time.
/// The previous entry stream is disposed before the next entry is opened, so a
/// census package is not held as a dictionary of every file plus a second copy
/// of the ZIP.
/// </summary>
public static class SubmissionZipWriter
{
    public readonly record struct Entry(string Name, Func<CancellationToken, Task<Stream>> OpenRead);

    public static async Task WriteAsync(
        Stream destination,
        IAsyncEnumerable<Entry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(entries);

        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var entry in entries.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            if (!seen.Add(entry.Name))
                throw new InvalidOperationException($"Duplicate submission file name '{entry.Name}'.");

            var zipEntry = zip.CreateEntry(entry.Name, CompressionLevel.Optimal);
            await using var source = await entry.OpenRead(cancellationToken).ConfigureAwait(false);
            await using var target = zipEntry.Open();
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
    }
}
