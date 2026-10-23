using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Import;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.PatientFlowSimulator;

[Trait("Category", "UnitTests")]
public class ImportRunnerTests : IDisposable
{
    private static readonly YearMonth November = new(2026, 11);

    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "pfs-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_DryRun_CountsOnlyFilesWhoseHourHasElapsedAndHaveNoMarker()
    {
        var output = new HourlyBundleBuilder(DefaultScenario.Load()).Build(November);
        MonthWriter.Write(output, _dataDirectory, overwrite: false);
        var now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var expected = output.Files.Count(f => f.Hour.AddHours(1) <= now);
        File.WriteAllText(Path.Combine(_dataDirectory, "2026-11", output.Files[0].FileName + ".done"), "{}");

        var result = await new ImportRunner(Options(now), _ => { }).RunAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(expected - 1, result.Imported);
    }

    [Fact]
    public async Task RunAsync_BeforeTheMonthStarts_HasNothingDue()
    {
        var output = new HourlyBundleBuilder(DefaultScenario.Load()).Build(November);
        MonthWriter.Write(output, _dataDirectory, overwrite: false);

        var result = await new ImportRunner(Options(output.Files[0].Hour), _ => { }).RunAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Imported);
    }

    [Fact]
    public async Task RunAsync_MissingMonth_IsGeneratedBeforeImporting()
    {
        var now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        var result = await new ImportRunner(Options(now, generate: true), _ => { }).RunAsync(CancellationToken.None);

        Assert.Equal(["2026-11"], result.GeneratedMonths);
        Assert.True(File.Exists(Path.Combine(_dataDirectory, "2026-11", MonthWriter.ManifestFileName)));
        Assert.True(result.Imported > 0);
    }

    private ImportOptions Options(DateTimeOffset now, bool generate = false) => new()
    {
        DataDirectory = _dataDirectory,
        ScenarioPath = DefaultScenario.Path,
        FhirBaseUrl = "http://localhost:1/fhir",
        Now = now,
        GenerateMissingMonths = generate,
        DryRun = true
    };
}
