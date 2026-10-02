using Automation.UI.Services;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class TempFileResponseTests
{
    [Fact]
    public async Task CopyAndDeleteAsync_CopiesAsynchronouslyAndDeletesTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "link-export-test-" + Guid.NewGuid().ToString("N") + ".zip");
        await File.WriteAllTextAsync(path, "diagnostics");
        await using var destination = new SyncWriteForbiddenStream();

        await TempFileResponse.CopyAndDeleteAsync(path, destination, CancellationToken.None);

        Assert.False(File.Exists(path));
        Assert.Equal("diagnostics", System.Text.Encoding.UTF8.GetString(destination.ToArray()));
    }

    private sealed class SyncWriteForbiddenStream : Stream
    {
        private readonly MemoryStream _inner = new();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public byte[] ToArray() => _inner.ToArray();
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("Synchronous operations are disallowed.");
        public override void Write(ReadOnlySpan<byte> buffer) =>
            throw new InvalidOperationException("Synchronous operations are disallowed.");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }
}
