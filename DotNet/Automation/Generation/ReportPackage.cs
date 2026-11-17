using System.IO.Compression;
using System.Text;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// A submission ZIP stored on disk. Callers read one entry at a time.
/// The ZIP bytes and the text of every entry are not held together.
/// </summary>
public sealed class ReportPackage : IDisposable, IAsyncDisposable
{
    private readonly string _path;
    private readonly bool _deleteOnDispose;
    private readonly FileStream _stream;
    private readonly ZipArchive _archive;
    private bool _disposed;

    private ReportPackage(string path, bool deleteOnDispose)
    {
        _path = path;
        _deleteOnDispose = deleteOnDispose;
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        try
        {
            _archive = new ZipArchive(_stream, ZipArchiveMode.Read, leaveOpen: false);
            EntryNames = _archive.Entries
                .Where(entry => entry.Length > 0 && !string.IsNullOrEmpty(entry.Name) && !entry.FullName.EndsWith('/'))
                .Select(entry => entry.FullName)
                .ToList();
        }
        catch
        {
            _stream.Dispose();
            throw;
        }
    }

    /// <summary>Zip entry names, excluding empty and directory entries.</summary>
    public IReadOnlyList<string> EntryNames { get; }

    internal string FilePath => _path;

    public static ReportPackage Open(string path, bool deleteOnDispose) => new(path, deleteOnDispose);

    public static string CreateTempPath() =>
        Path.Combine(Path.GetTempPath(), "link-report-" + Guid.NewGuid().ToString("N") + ".zip");

    public static bool HasZipHeader(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == 4
            && header[0] == 0x50
            && header[1] == 0x4B
            && header[2] == 0x03
            && header[3] == 0x04;
    }

    /// <summary>
    /// Builds a package from entry text. Used by callers that already hold a
    /// small in-memory fixture. The text is written to a temp ZIP and then
    /// dropped; later reads come back from that file one entry at a time.
    /// </summary>
    public static ReportPackage FromTextEntries(IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var path = CreateTempPath();
        try
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, text) in entries)
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(text ?? string.Empty);
                }
            }

            return new ReportPackage(path, deleteOnDispose: true);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    public bool TryMatchEntry(string expectedFileName, out string? matchedKey)
    {
        ThrowIfDisposed();
        foreach (var name in EntryNames)
        {
            if (string.Equals(name, expectedFileName, StringComparison.Ordinal))
            {
                matchedKey = name;
                return true;
            }
        }

        var suffix = "_" + expectedFileName;
        foreach (var name in EntryNames)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                matchedKey = name;
                return true;
            }
        }

        matchedKey = null;
        return false;
    }

    /// <summary>
    /// Reads one entry into a string. The caller should drop that string
    /// before reading the next entry.
    /// </summary>
    public string? ReadEntryText(string fullName)
    {
        ThrowIfDisposed();
        var entry = _archive.GetEntry(fullName);
        if (entry == null)
            return null;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Copies one entry to <paramref name="destination"/> in 80 KB chunks.
    /// The returned count is the number of decoded characters, matching
    /// <see cref="ReadEntryText"/>. The destination stays open.
    /// </summary>
    public async Task<int?> CopyEntryTextToAsync(
        string fullName,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ThrowIfDisposed();
        var entry = _archive.GetEntry(fullName);
        if (entry == null)
            return null;

        await using var source = entry.Open();
        using var reader = new StreamReader(source);
        await using var writer = new StreamWriter(
            destination,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 81920,
            leaveOpen: true);

        var buffer = new char[81920];
        var charCount = 0;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            charCount += read;
            await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return charCount;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _archive.Dispose();
        _stream.Dispose();
        if (_deleteOnDispose)
            TryDelete(_path);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ReportPackage));
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
            // A failed temp-file delete must not hide the download or validation error.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
