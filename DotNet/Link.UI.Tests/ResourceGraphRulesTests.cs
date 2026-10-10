using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ResourceGraphRulesTests
{
    [Fact]
    public void A_page_is_bounded_and_a_cap_stops_the_walk()
    {
        var spec = new GraphSpec("patient-1", [("Observation", 30), ("Encounter", 1)]);
        var capped = ResourceGraphRules.Build(spec, CancellationToken.None, 10);
        capped.Total.Should().Be(10);
        capped.Truncated.Should().BeTrue();

        var index = ResourceGraphRules.Build(spec, CancellationToken.None);
        index.Total.Should().Be(31);
        var wide = index.Page("Observation", null, 1, 500);
        wide.Records.Should().HaveCount(30);
        wide.Metadata.PageSize.Should().Be(ResourceGraphRules.MaxPageSize);
        index.Page("Observation", null, 2, 25).Metadata.TotalCount.Should().Be(30);
        index.Page("Observation", null, 2, 25).Records.Should().HaveCount(5);
        var first = index.Page("Observation", null, 1, 25).Records[0];
        index.Page("Observation", first.Id, 1, 25).Records.Should().ContainSingle();
        index.Page("Missing", null, 1, 25).Records.Should().BeEmpty();
        index.Page("Missing", null, 1, 25).Metadata.TotalCount.Should().Be(0);

        var raw = index.Raw("Observation", first.Id);
        raw.Should().NotBeNull();
        raw!.Json.Should().Contain("\"resourceType\": \"Observation\"");
        raw.Refs.Should().Contain(item => item.StartsWith("Patient/", StringComparison.Ordinal));
        raw.Refs.Should().Contain(item => item.StartsWith("Encounter/", StringComparison.Ordinal));
        index.Raw("Observation", "missing").Should().BeNull();
    }

    [Fact]
    public void A_cancelled_build_stops_and_a_bundle_is_read_forward_only()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => ResourceGraphRules.Build(new GraphSpec("p", [("Observation", 5)]), cts.Token);
        act.Should().Throw<OperationCanceledException>();

        var json = """
            {"resourceType":"Bundle","entry":[
              {"resource":{"resourceType":"Patient","id":"p1"}},
              {"resource":{"resourceType":"Observation","id":"o1","subject":{"reference":"Patient/p1"},"encounter":{"reference":"http://example/Encounter/e1"}}},
              {"resource":{"resourceType":"Encounter","id":"e1","subject":{"reference":"Patient/p1"}}}
            ]}
            """;
        var index = ResourceGraphRules.ReadBundle(json, "p1", CancellationToken.None);
        index.Error.Should().BeNull();
        index.Total.Should().Be(2);
        index.Types.Select(type => type.Name).Should().Equal("Encounter", "Observation");
        var raw = index.Raw("Observation", "o1");
        raw!.Refs.Should().Contain("Patient/p1").And.Contain("Encounter/e1");
        raw.Json.Should().Contain("o1");
        index.Raw("Patient", "p1").Should().BeNull();

        var coded = """
            {"resourceType":"Bundle","entry":[
              {"resource":{"resourceType":"Observation","id":"o1","code":{"text":"ZZ-NEEDLE-9"},"subject":{"reference":"Patient/p1"}}},
              {"resource":{"resourceType":"Observation","id":"o2","subject":{"reference":"Patient/p1"}}}
            ]}
            """;
        var codedIndex = ResourceGraphRules.ReadBundle(coded, "p1", CancellationToken.None);
        var hit = codedIndex.Page("Observation", "ZZ-NEEDLE-9", 1, 25);
        hit.Metadata.TotalCount.Should().Be(1);
        hit.Records.Should().ContainSingle(row => row.Id == "o1");
        hit.Records[0].Snippet.Should().Contain("ZZ-NEEDLE-9");
        hit.MatchedTypes.Should().ContainSingle(type => type.Name == "Observation" && type.Count == 1);
        codedIndex.Page("Observation", "not-in-the-bundle", 1, 25).Metadata.TotalCount.Should().Be(0);

        ResourceGraphRules.ReadBundle("{", "p1", CancellationToken.None).Error.Should().Be("The bundle could not be read.");
        ResourceGraphRules.ReadBundle(new string('x', 5), "p1", CancellationToken.None, 2).Total.Should().Be(0);

        var tiny = ResourceGraphRules.ReadBundle(json, "p1", CancellationToken.None, 1);
        tiny.Total.Should().Be(1);
        tiny.Truncated.Should().BeTrue();
    }

    [Fact]
    public void Fifteen_thousand_resources_and_five_thousand_patients_stay_paged()
    {
        ResourceGraphRules.ScaleMixTotal.Should().Be(15_000);

        var graphWatch = Stopwatch.StartNew();
        var graph = ResourceGraphRules.Build(
            new GraphSpec(ResourceGraphRules.ScalePatientId, ResourceGraphRules.ScaleMix),
            CancellationToken.None);
        graphWatch.Stop();
        graph.Total.Should().Be(15_000);
        graph.Truncated.Should().BeFalse();
        graph.Types[0].Name.Should().Be("Observation");
        graph.Types[0].Count.Should().Be(9_000);
        graph.Page("Observation", null, 40, 25).Records.Should().HaveCount(25);
        graph.Page("Observation", null, 1, 500).Records.Should().HaveCount(ResourceGraphRules.MaxPageSize);
        graph.Page("Observation", null, 40, 25).Metadata.PageNumber.Should().Be(40);
        graphWatch.ElapsedMilliseconds.Should().BeLessThan(1_000);

        var filterWatch = Stopwatch.StartNew();
        var filtered = graph.Page("Observation", "final", 1, 25);
        filterWatch.Stop();
        filtered.Metadata.TotalCount.Should().Be(9_000);
        filtered.Metadata.PageNumber.Should().Be(1);
        filtered.Records.Should().HaveCount(25);
        filtered.Records.Should().OnlyContain(row => row.Snippet != null && row.Snippet.Contains("final", StringComparison.OrdinalIgnoreCase));
        filtered.MatchedTypes.Should().NotBeNull();
        filtered.MatchedTypes!.Sum(type => type.Count).Should().Be(15_000);
        filterWatch.ElapsedMilliseconds.Should().BeLessThan(1_000);
        var known = graph.Page("Observation", null, 360, 25).Records[0];
        var one = graph.Page("Observation", known.Id, 1, 25);
        one.Metadata.TotalCount.Should().Be(1);
        one.Records.Should().ContainSingle(row => row.Id == known.Id);

        var bundle = new StringBuilder();
        bundle.Append("{\"resourceType\":\"Bundle\",\"entry\":[");
        for (var index = 0; index < 15_000; index++)
        {
            if (index > 0)
                bundle.Append(',');
            bundle.Append("{\"resource\":{\"resourceType\":\"Observation\",\"id\":\"n")
                .Append(index)
                .Append("\",\"subject\":{\"reference\":\"Patient/p\"}}}");
        }
        bundle.Append("]}");
        var readWatch = Stopwatch.StartNew();
        var read = ResourceGraphRules.ReadBundle(bundle.ToString(), "p", CancellationToken.None);
        readWatch.Stop();
        read.Error.Should().BeNull();
        read.Total.Should().Be(15_000);
        read.Truncated.Should().BeFalse();
        readWatch.ElapsedMilliseconds.Should().BeLessThan(5_000);

        var pageWatch = Stopwatch.StartNew();
        var page = ReportManifestRules.ScaleReport(new ReportManifestQuery { PageSize = 25, Descending = true });
        pageWatch.Stop();
        var model = page.Manifest!;
        model.PatientCount.Should().Be(5_000);
        model.Patients.Should().HaveCount(25);
        model.Patients[0].PatientId.Should().Be(ResourceGraphRules.ScalePatientId);
        model.Patients[0].Total.Should().Be(15_000);
        model.Patients[0].ResourceRefs.Should().BeEmpty();
        model.TotalResourceCount.Should().Be(ResourceGraphRules.ScaleResourceTotal());
        model.Populations.Should().ContainSingle();
        model.Populations[0].Rate.Should().Be("53.8%");
        model.Route["scale"].Should().Be("1");
        pageWatch.ElapsedMilliseconds.Should().BeLessThan(500);

        ResourceGraphRules.TryFixture(ResourceGraphRules.ScalePatientId, scale: true, out var heavy).Should().BeTrue();
        heavy.Counts.Sum(pair => pair.Count).Should().Be(15_000);
        ResourceGraphRules.TryFixture("patient-00002", scale: true, out _).Should().BeTrue();
        ResourceGraphRules.TryFixture("patient-01", scale: false, out _).Should().BeFalse();
        ResourceGraphRules.TryFixture("11111111-1111-1111-1111-111111111112", scale: false, out var sample).Should().BeTrue();
        sample.Counts.Sum(pair => pair.Count).Should().Be(41);

        var numerator = ReportManifestRules.ScaleReport(ReportManifestRules.Normalize(
            null, null, null, null, null, 1, 25, 1, 25, 1, 25, 1, "total", "numerator", null, 1, "populations")).Manifest!;
        numerator.PopulationPaging.TotalCount.Should().Be(2_100);
        numerator.PopulationPatients.Should().HaveCount(25);
        numerator.PopulationPatients[0].PatientId.Should().Be(ResourceGraphRules.ScalePatientId);
    }
}
