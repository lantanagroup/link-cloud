namespace LantanaGroup.Link.Submission.Application.Services;

/// <summary>
/// Builds a submission ZIP on disk, then copies it to the caller asynchronously.
/// ZipArchive writes its central directory with synchronous <see cref="Stream.Write"/>,
/// which Kestrel rejects on <c>HttpResponse.Body</c>.
/// </summary>
public static class SubmissionZipResponse
{
    public static async Task CopyToAsync(
        Stream destination,
        Func<Stream, CancellationToken, Task> writeZip,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(writeZip);

        var tempPath = Path.Combine(Path.GetTempPath(), "link-submission-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await writeZip(file, cancellationToken).ConfigureAwait(false);
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var read = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            await read.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
