using System.Text;
using LantanaGroup.Automation.Generation;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class ReportPackageTests
{
    [Fact]
    public void ReadEntryText_ReadsOneEntryAndDisposeDeletesTheFile()
    {
        var package = ReportPackage.FromTextEntries(
        [
            new KeyValuePair<string, string>("manifest.ndjson", "{\"resourceType\":\"List\"}\n"),
            new KeyValuePair<string, string>("patient-p1.ndjson", "{\"resourceType\":\"Patient\",\"id\":\"p1\"}\n")
        ]);

        var path = package.FilePath;
        Assert.True(File.Exists(path));
        Assert.Equal(["manifest.ndjson", "patient-p1.ndjson"], package.EntryNames);
        Assert.Equal("{\"resourceType\":\"Patient\",\"id\":\"p1\"}\n", package.ReadEntryText("patient-p1.ndjson"));
        Assert.Null(package.ReadEntryText("missing.ndjson"));

        package.Dispose();

        Assert.False(File.Exists(path));
        Assert.Throws<ObjectDisposedException>(() => package.ReadEntryText("patient-p1.ndjson"));
    }

    [Fact]
    public void TryMatchEntry_MatchesFlattenedExternalNameBySuffix()
    {
        using var package = ReportPackage.FromTextEntries(
        [
            new KeyValuePair<string, string>("report_manifest.ndjson", "{\"resourceType\":\"List\"}\n"),
            new KeyValuePair<string, string>("report_patient-p1.ndjson", "{\"resourceType\":\"Patient\"}\n")
        ]);

        Assert.True(package.TryMatchEntry("manifest.ndjson", out var manifest));
        Assert.Equal("report_manifest.ndjson", manifest);
        Assert.True(package.TryMatchEntry("patient-p1.ndjson", out var patient));
        Assert.Equal("report_patient-p1.ndjson", patient);
        Assert.False(package.TryMatchEntry("patient-missing.ndjson", out _));
    }

    [Fact]
    public void AbsUploadSnapshot_CountsOnePatientFileWithoutRetainingTheText()
    {
        using var package = ReportPackage.FromTextEntries(
        [
            new KeyValuePair<string, string>(
                "manifest.ndjson",
                "{\"resourceType\":\"Organization\"}\n{\"resourceType\":\"List\"}\n"),
            new KeyValuePair<string, string>(
                "patient-abc.ndjson",
                "{\"resourceType\":\"Patient\"}\n{\"resourceType\":\"Observation\"}\n")
        ]);

        var snapshot = AbsUploadSnapshot.Build(package);

        Assert.Equal(["abc"], snapshot.PatientIds);
        Assert.Equal(2, snapshot.TotalResourceCount);
        Assert.Equal(2, snapshot.ManifestResourceCount);
        Assert.Equal(1, snapshot.TotalCountsByType["Observation"]);
    }

    [Fact]
    public async Task CopyEntryTextToAsync_CopiesOneEntryAndReturnsTheCharacterCount()
    {
        const string text = "{\"resourceType\":\"Patient\",\"id\":\"p1\"}\n";
        await using var package = ReportPackage.FromTextEntries(
        [
            new KeyValuePair<string, string>("patient-p1.ndjson", text)
        ]);

        using var destination = new MemoryStream();
        var count = await package.CopyEntryTextToAsync("patient-p1.ndjson", destination, CancellationToken.None);

        Assert.Equal(text.Length, count);
        Assert.Equal(text, Encoding.UTF8.GetString(destination.ToArray()));
        Assert.Null(await package.CopyEntryTextToAsync("missing.ndjson", destination, CancellationToken.None));
    }

    [Fact]
    public async Task CopyEntryTextToAsync_CancellationStopsBeforeTheDestinationIsWritten()
    {
        await using var package = ReportPackage.FromTextEntries(
        [
            new KeyValuePair<string, string>("patient-p1.ndjson", "{\"resourceType\":\"Patient\"}\n")
        ]);
        using var destination = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            package.CopyEntryTextToAsync("patient-p1.ndjson", destination, cts.Token));

        Assert.Equal(0, destination.Length);
    }
}
