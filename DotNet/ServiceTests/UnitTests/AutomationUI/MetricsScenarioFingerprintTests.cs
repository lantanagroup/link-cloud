using Automation.UI.Services;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MetricsScenarioFingerprintTests
{
    [Fact]
    public void Same_setup_produces_the_same_fingerprint()
    {
        var a = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null);
        var b = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null);
        a.Should().Be(b);
        a.Should().HaveLength(12);
    }

    [Fact]
    public void Patient_count_change_produces_a_new_fingerprint()
    {
        var a = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, null, null, null, null, null);
        var b = MetricsScenarioFingerprint.Compute(300, 1, 25, 50, 8, null, null, null, null, null);
        a.Should().NotBe(b);
    }

    [Fact]
    public void NextVersion_stays_when_fingerprint_matches()
    {
        MetricsScenarioFingerprint.NextVersion("aaa", 2, "aaa").Should().Be(2);
    }

    [Fact]
    public void NextVersion_increments_when_setup_changes()
    {
        MetricsScenarioFingerprint.NextVersion("aaa", 2, "bbb").Should().Be(3);
    }

    [Fact]
    public void Different_measure_template_ids_produce_a_new_fingerprint()
    {
        var a = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null, ["id-a"], "hash-a");
        var b = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null, ["id-b"], "hash-a");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Different_measure_bundle_hashes_produce_a_new_fingerprint()
    {
        var a = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null, ["id-a"], "hash-a");
        var b = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null, ["id-a"], "hash-b");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Different_thetis_versions_produce_a_new_fingerprint()
    {
        var a = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], null, null, null, ["id-a"], "hash-a", "1.0.0");
        var b = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], null, null, null, ["id-a"], "hash-a", "1.0.1");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Different_patient_shape_keys_produce_a_new_fingerprint()
    {
        var a = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null, ["id-a"], "hash-a", "1.0.0", ["cfg-a"]);
        var b = MetricsScenarioFingerprint.Compute(150, 1, 25, 50, 8, "k", ["ACH"], "abc", null, null, ["id-a"], "hash-a", "1.0.0", ["cfg-b"]);
        a.Should().NotBe(b);
    }

    [Fact]
    public void HashMeasureBundles_is_stable_for_the_same_json()
    {
        var hash = MetricsScenarioFingerprint.HashMeasureBundles(["{\"a\":1}"]);
        hash.Should().Be(MetricsScenarioFingerprint.HashMeasureBundles(["{\"a\":1}"]));
        hash.Should().NotBe(MetricsScenarioFingerprint.HashMeasureBundles(["{\"a\":2}"]));
    }

    [Fact]
    public void Describe_is_readable()
    {
        var text = MetricsScenarioFingerprint.Describe(150, 20260329, 25, 50, 8);
        text.Should().Contain("150 patients");
        text.Should().Contain("seed 20260329");
        text.Should().Contain("25–50");
        text.Should().Contain("8 queries at a time");
    }
}
