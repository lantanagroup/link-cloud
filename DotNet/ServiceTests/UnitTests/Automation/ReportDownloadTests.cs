using System.IO.Compression;
using System.Text;
using LantanaGroup.Automation.Generation;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Services;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ReportDownloadTests
{
    [Fact]
    public async Task DownloadReportAsync_StreamsTheZipAndDoesNotBufferTheBody()
    {
        var client = new StreamingSubmissionClient((destination, _) =>
        {
            WriteZip(destination, ("patient-p1.ndjson", "{\"resourceType\":\"Patient\"}\n"), ("manifest.ndjson", "{\"resourceType\":\"List\"}\n"));
            return Task.FromResult(new SubmissionDownloadResult(200, null));
        });
        var output = new ListOutput();
        var helper = CreateHelper(client, new AutomationConfig(), output);

        var package = await helper.DownloadReportAsync("facility", "report-1", new TestScenarioConfig());

        Assert.Equal(0, client.BufferedCalls);
        Assert.Contains(output.Lines, line => line.Contains("Downloading report report-1", StringComparison.Ordinal));
        Assert.Equal("{\"resourceType\":\"Patient\"}\n", package.ReadEntryText("patient-p1.ndjson"));
        var path = package.FilePath;
        await package.DisposeAsync();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DownloadReportAsync_CopiesTheZipWhenADownloadPathIsConfigured()
    {
        var directory = Path.Combine(Path.GetTempPath(), "link-report-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var client = new StreamingSubmissionClient((destination, _) =>
            {
                WriteZip(destination, ("manifest.ndjson", "{\"resourceType\":\"List\"}\n"));
                return Task.FromResult(new SubmissionDownloadResult(200, null));
            });
            var helper = CreateHelper(client, new AutomationConfig { DownloadPath = directory });
            var config = new TestScenarioConfig { DownloadFileName = "saved.zip" };

            await using var package = await helper.DownloadReportAsync("facility", "report-1", config);

            var saved = Path.Combine(directory, "saved.zip");
            Assert.True(File.Exists(saved));
            Assert.True(ReportPackage.HasZipHeader(saved));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadReportAsync_FailedStatusThrowsAndDoesNotSaveAFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "link-report-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var client = new StreamingSubmissionClient((_, _) =>
                Task.FromResult(new SubmissionDownloadResult(503, "unavailable")));
            var helper = CreateHelper(client, new AutomationConfig { DownloadPath = directory });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                helper.DownloadReportAsync("facility", "report-1", new TestScenarioConfig { DownloadFileName = "saved.zip" }));

            Assert.Contains("503", ex.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(directory, "saved.zip")));
            Assert.Equal(0, client.BufferedCalls);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadReportAsync_NonZipPayloadThrows()
    {
        var client = new StreamingSubmissionClient((destination, _) =>
        {
            destination.Write("hello"u8);
            return Task.FromResult(new SubmissionDownloadResult(200, null));
        });
        var helper = CreateHelper(client, new AutomationConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            helper.DownloadReportAsync("facility", "report-1", new TestScenarioConfig()));

        Assert.Contains("not a ZIP", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadReportAsync_CancelledSaveDeletesThePartialFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "link-report-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var client = new StreamingSubmissionClient((destination, _) =>
            {
                WriteZip(destination, ("manifest.ndjson", "{\"resourceType\":\"List\"}\n"));
                return Task.FromResult(new SubmissionDownloadResult(200, null));
            });
            var helper = CreateHelper(client, new AutomationConfig { DownloadPath = directory });
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                helper.DownloadReportAsync(
                    "facility",
                    "report-1",
                    new TestScenarioConfig { DownloadFileName = "saved.zip" },
                    cancellationToken: cts.Token));

            Assert.False(File.Exists(Path.Combine(directory, "saved.zip")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ReportApiHelper CreateHelper(
        StreamingSubmissionClient client,
        AutomationConfig config,
        ListOutput? output = null) =>
        new(null!, null!, client, null!, output ?? new ListOutput(), config, null!);

    private static void WriteZip(Stream destination, params (string Name, string Text)[] files)
    {
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var (name, text) in files)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(text);
        }
    }

    private sealed class StreamingSubmissionClient : ISubmissionServiceClient
    {
        private readonly Func<Stream, CancellationToken, Task<SubmissionDownloadResult>> _copy;

        public StreamingSubmissionClient(Func<Stream, CancellationToken, Task<SubmissionDownloadResult>> copy) =>
            _copy = copy;

        public int BufferedCalls { get; private set; }

        public Task<LinkApiResponse<byte[]>> DownloadSubmissionAsync(
            string facilityId, string reportId, bool external = true, CancellationToken cancellationToken = default)
        {
            BufferedCalls++;
            throw new InvalidOperationException("DownloadSubmissionAsync buffers the ZIP.");
        }

        public Task<SubmissionDownloadResult> CopySubmissionAsync(
            string facilityId,
            string reportId,
            Stream destination,
            bool external = true,
            CancellationToken cancellationToken = default) =>
            _copy(destination, cancellationToken);
    }

    private sealed class ListOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
