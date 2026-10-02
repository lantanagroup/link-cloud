using System.IO.Compression;
using System.Text;
using LantanaGroup.Link.Submission.Application.Services;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Submission;

[Trait("Category", "UnitTests")]
public class SubmissionZipWriterTests
{
    [Fact]
    public async Task WriteAsync_OpensTheNextBlobOnlyAfterThePreviousStreamIsDisposed()
    {
        var firstDisposed = false;
        var secondOpenedBeforeDispose = false;

        async IAsyncEnumerable<SubmissionZipWriter.Entry> Entries()
        {
            yield return new SubmissionZipWriter.Entry("a.ndjson", _ =>
                Task.FromResult<Stream>(new FlagStream("{\"resourceType\":\"Patient\"}"u8.ToArray(), () => firstDisposed = true)));
            yield return new SubmissionZipWriter.Entry("b.ndjson", _ =>
            {
                if (!firstDisposed)
                    secondOpenedBeforeDispose = true;
                return Task.FromResult<Stream>(new MemoryStream("{\"resourceType\":\"Observation\"}"u8.ToArray()));
            });
        }

        using var destination = new MemoryStream();
        await SubmissionZipWriter.WriteAsync(destination, Entries(), CancellationToken.None);

        Assert.False(secondOpenedBeforeDispose);
        Assert.True(firstDisposed);

        using var zip = new ZipArchive(destination, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Equal("{\"resourceType\":\"Patient\"}", await ReadAsync(zip, "a.ndjson"));
        Assert.Equal("{\"resourceType\":\"Observation\"}", await ReadAsync(zip, "b.ndjson"));
    }

    [Fact]
    public async Task WriteAsync_DuplicateFileNameThrowsBeforeOpeningTheDuplicate()
    {
        var secondOpened = false;

        async IAsyncEnumerable<SubmissionZipWriter.Entry> Entries()
        {
            yield return new SubmissionZipWriter.Entry("patient-1.ndjson", _ =>
                Task.FromResult<Stream>(new MemoryStream("one"u8.ToArray())));
            yield return new SubmissionZipWriter.Entry("patient-1.ndjson", _ =>
            {
                secondOpened = true;
                return Task.FromResult<Stream>(new MemoryStream("two"u8.ToArray()));
            });
        }

        using var destination = new MemoryStream();
        var act = () => SubmissionZipWriter.WriteAsync(destination, Entries(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("patient-1.ndjson", ex.Message, StringComparison.Ordinal);
        Assert.False(secondOpened);
    }

    [Fact]
    public async Task WriteAsync_CancellationStopsBeforeTheNextEntryIsOpened()
    {
        using var cts = new CancellationTokenSource();
        var opened = 0;

        async IAsyncEnumerable<SubmissionZipWriter.Entry> Entries()
        {
            yield return new SubmissionZipWriter.Entry("a.ndjson", _ =>
            {
                opened++;
                cts.Cancel();
                return Task.FromResult<Stream>(new MemoryStream("a"u8.ToArray()));
            });
            yield return new SubmissionZipWriter.Entry("b.ndjson", _ =>
            {
                opened++;
                return Task.FromResult<Stream>(new MemoryStream("b"u8.ToArray()));
            });
        }

        using var destination = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SubmissionZipWriter.WriteAsync(destination, Entries(), cts.Token));

        Assert.Equal(1, opened);
    }

    private static async Task<string> ReadAsync(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private sealed class FlagStream : MemoryStream
    {
        private readonly Action _onDispose;

        public FlagStream(byte[] data, Action onDispose) : base(data)
        {
            _onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _onDispose();
            base.Dispose(disposing);
        }
    }
}
