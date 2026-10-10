using System.Diagnostics;
using System.Text;
using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Utilities;
using Link.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Link.UI.Tests;

public class SubmissionPatientBlobTests
{
    private readonly ITestOutputHelper _output;

    public SubmissionPatientBlobTests(ITestOutputHelper output) => _output = output;
    [Fact]
    public void External_name_follows_the_submission_copy()
    {
        var schedule = Schedule("https://account.blob.core.windows.net/internal/reports/hospital_hypo_20260115_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        SubmissionBlobNames.TryPatientFile("patient-1", out var file).Should().BeTrue();
        file.Should().Be("patient-patient-1.ndjson");

        var plain = SubmissionBlobNames.ExternalName(new SubmissionReadOptions { InternalBlobRoot = "reports" }, schedule, file);
        plain.Should().Be("hospital_hypo_20260115_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/patient-patient-1.ndjson");

        var rooted = SubmissionBlobNames.ExternalName(new SubmissionReadOptions
        {
            InternalBlobRoot = "reports",
            ExternalBlobRoot = "out"
        }, schedule, file);
        rooted.Should().Be("out/hospital_hypo_20260115_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/patient-patient-1.ndjson");

        var flat = SubmissionBlobNames.ExternalName(new SubmissionReadOptions
        {
            InternalBlobRoot = "reports",
            ExternalBlobRoot = "out",
            FlattenHierarchy = true,
            UseMeasurePrefix = true,
            MeasurePrefixesByReportType = new Dictionary<string, string>
            {
                ["NHSNGlycemicControlHypoglycemicInitialPopulation"] = "hypo"
            }
        }, schedule, file);
        flat.Should().Be("out/hypo/hospital_hypo_20260115_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee_patient-patient-1.ndjson");

        var azurite = SubmissionBlobNames.ExternalName(
            new SubmissionReadOptions(),
            Schedule("http://127.0.0.1:10000/devstoreaccount1/internal/hospital_hypo_20260115_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            file);
        azurite.Should().Be("hospital_hypo_20260115_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/patient-patient-1.ndjson");
    }

    [Fact]
    public void A_missing_payload_folder_uses_the_report_name()
    {
        var schedule = Schedule(null);
        SubmissionBlobNames.TryPatientFile("p1", out var file).Should().BeTrue();
        var expected = ReportHelpers.GetReportName(schedule.Id, schedule.FacilityId, schedule.ReportTypes, schedule.ReportStartDate);
        SubmissionBlobNames.ExternalName(new SubmissionReadOptions(), schedule, file)
            .Should().Be(expected + "/patient-p1.ndjson");
    }

    [Fact]
    public void A_patient_id_with_a_slash_is_not_a_blob_name()
    {
        SubmissionBlobNames.TryPatientFile("../secret", out _).Should().BeFalse();
        SubmissionBlobNames.TryPatientFile("a/b", out _).Should().BeFalse();
        SubmissionBlobNames.TryPatientFile("ok-id.1", out var file).Should().BeTrue();
        file.Should().Be("patient-ok-id.1.ndjson");
    }

    [Fact]
    public async Task Ndjson_is_streamed_without_keeping_bodies()
    {
        var bytes = Encoding.UTF8.GetBytes(
            "{\"resourceType\":\"Patient\",\"id\":\"p1\"}\n"
            + "{\"resourceType\":\"Observation\",\"id\":\"o1\",\"subject\":{\"reference\":\"Patient/p1\"}}\n"
            + "not-json\n"
            + "{\"resourceType\":\"Encounter\",\"id\":\"e1\",\"subject\":{\"reference\":\"Patient/p1\"}}\n");
        var index = await ResourceGraphRules.ReadNdjsonAsync(new MemoryStream(bytes), "p1", CancellationToken.None);
        index.Origin.Should().Be("submission");
        index.Error.Should().BeNull();
        index.Total.Should().Be(2);
        index.Types.Select(type => type.Name).Should().Equal("Encounter", "Observation");
        index.Raw("Patient", "p1").Should().BeNull();

        var raw = index.Raw("Observation", "o1");
        raw.Should().NotBeNull();
        raw!.Json.Should().BeNull();
        raw.Refs.Should().Contain("Patient/p1");
        index.TrySpan("Observation", "o1", out var offset, out var length).Should().BeTrue();
        var body = Encoding.UTF8.GetString(bytes, (int)offset, length);
        ResourceGraphIndex.WithBody(raw, body).Json.Should().Contain("\"id\": \"o1\"");

        var capped = await ResourceGraphRules.ReadNdjsonAsync(new MemoryStream(bytes), "p1", CancellationToken.None, 1);
        capped.Total.Should().Be(1);
        capped.Truncated.Should().BeTrue();

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var act = () => ResourceGraphRules.ReadNdjsonAsync(new MemoryStream(bytes), "p1", cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Fifteen_thousand_submission_lines_stay_bounded()
    {
        var patient = "{\"resourceType\":\"Patient\",\"id\":\"p\"}\n";
        var line = "{\"resourceType\":\"Observation\",\"id\":\"o\",\"subject\":{\"reference\":\"Patient/p\"}}\n";
        using var stream = new MemoryStream();
        var prefix = Encoding.UTF8.GetBytes(patient);
        stream.Write(prefix);
        var row = Encoding.UTF8.GetBytes(line);
        for (var i = 0; i < 15_000; i++)
            stream.Write(row);
        stream.Position = 0;

        var watch = Stopwatch.StartNew();
        var index = await ResourceGraphRules.ReadNdjsonAsync(stream, "p", CancellationToken.None);
        watch.Stop();
        index.Total.Should().Be(15_000);
        index.Truncated.Should().BeFalse();
        index.Raw("Observation", "o")!.Json.Should().BeNull();
        index.Page("Observation", null, 1, 25).Records.Should().HaveCount(25);
        index.Page("Observation", null, 360, 25).Metadata.TotalPages.Should().Be(600);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        _output.WriteLine("15k submission lines parsed in " + watch.ElapsedMilliseconds + " ms");
    }

    [Fact]
    public async Task A_missing_submission_falls_through_and_a_present_one_is_read()
    {
        var schedule = Schedule("https://account.blob.core.windows.net/internal/hospital_hypo_20260115_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var options = Options.Create(new SubmissionReadOptions());
        var store = new MemoryBlobStore();
        var reader = new PatientSubmissionReader(options, store, NullLogger<PatientSubmissionReader>.Instance);
        SubmissionBlobNames.TryPatientFile("p1", out var file).Should().BeTrue();
        var name = SubmissionBlobNames.ExternalName(options.Value, schedule, file)!;

        (await reader.ReadAsync(schedule, "p1", null, null, CancellationToken.None)).Should().BeNull();
        store.Opens.Should().Be(1);

        (await reader.ReadAsync(schedule, "a/b", null, null, CancellationToken.None)).Should().BeNull();
        store.Opens.Should().Be(1);

        var payload = Encoding.UTF8.GetBytes("{\"resourceType\":\"Observation\",\"id\":\"o1\",\"subject\":{\"reference\":\"Patient/p1\"}}\n");
        store.Blobs[name] = payload;
        var index = await reader.ReadAsync(schedule, "p1", null, null, CancellationToken.None);
        index.Should().NotBeNull();
        index!.Origin.Should().Be("submission");
        index.Address!.Name.Should().Be(name);
        index.Total.Should().Be(1);
        index.TrySpan("Observation", "o1", out var offset, out var length).Should().BeTrue();
        var body = await reader.ReadBodyAsync(index.Address, offset, length, CancellationToken.None);
        body.Should().Contain("o1");

        store.IsConfigured = false;
        (await reader.ReadAsync(schedule, "p1", null, null, CancellationToken.None)).Should().BeNull();
    }

    private static ReportScheduleApiModel Schedule(string? payloadRoot) => new()
    {
        Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        FacilityId = "Hospital",
        ReportTypes = ["NHSNGlycemicControlHypoglycemicInitialPopulation"],
        ReportStartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        PayloadRootUri = payloadRoot
    };

    private sealed class MemoryBlobStore : ISubmissionBlobStore
    {
        public bool IsConfigured { get; set; } = true;
        public int Opens { get; private set; }
        public Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

        public Task<Stream?> OpenAsync(string blobName, CancellationToken cancellationToken)
        {
            Opens++;
            if (!Blobs.TryGetValue(blobName, out var bytes))
                return Task.FromResult<Stream?>(null);
            return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
        }

        public Task<string?> ReadRangeAsync(string blobName, long offset, int length, CancellationToken cancellationToken)
        {
            if (!Blobs.TryGetValue(blobName, out var bytes) || offset < 0 || offset > bytes.Length)
                return Task.FromResult<string?>(null);
            var take = (int)Math.Min(length, bytes.Length - offset);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes, (int)offset, take));
        }
    }
}
