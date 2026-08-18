using LantanaGroup.Link.Report.Application.Options;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Report;

/// <summary>
/// Covers the startup drift check for the paired pre-qualification flag (LEGLINK-466). Report and the
/// Java Validation service read separate keys for one decision, so a half-configured pair is possible
/// — and it fails silently, producing wrong submitted data rather than an error. This check is what
/// makes that state visible.
/// </summary>
[Trait("Category", "UnitTests")]
public class PreQualificationFlagConsistencyTests
{
    private const string ValidationKey = "/pre-qualification/write-pre-qual-operation-outcome";

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();
    }

    [Fact]
    public void ValidationEnabledWhileReportDisabled_IsAMismatch()
    {
        // The duplicate case: Validation writes its OperationOutcome and Report still appends its
        // legacy one, so the patient NDJSON ends up with two.
        var configuration = Configuration((ValidationKey, "true"));

        var mismatch = PreQualificationFlagConsistency.TryDetectMismatch(
            configuration, reportValue: false, out var validationValue);

        Assert.True(mismatch);
        Assert.True(validationValue);
    }

    [Fact]
    public void ReportEnabledWhileValidationDisabled_IsAMismatch()
    {
        // The inverse: Report skips its append and Validation never writes one, so the NDJSON carries
        // no pre-qualification OperationOutcome at all.
        var configuration = Configuration((ValidationKey, "false"));

        var mismatch = PreQualificationFlagConsistency.TryDetectMismatch(
            configuration, reportValue: true, out var validationValue);

        Assert.True(mismatch);
        Assert.False(validationValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MatchingValues_AreNotAMismatch(bool value)
    {
        var configuration = Configuration((ValidationKey, value ? "true" : "false"));

        Assert.False(PreQualificationFlagConsistency.TryDetectMismatch(configuration, value, out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidationKeyAbsent_IsNeverAMismatch(bool reportValue)
    {
        // Absence is not disagreement. Anywhere Azure App Configuration is not the source -- the local
        // docker-compose stack, where each container gets only its own environment variable -- this key
        // is simply not visible to Report. Warning on every local startup would train people to ignore
        // the message.
        var configuration = Configuration();

        Assert.False(PreQualificationFlagConsistency.TryDetectMismatch(configuration, reportValue, out var validationValue));
        Assert.False(validationValue);
    }

    [Fact]
    public void ReportKeyAbsentIsTreatedAsFalse()
    {
        // Report's absent row still means false, so "Report row missing, Validation row true" has to
        // register as the duplicate-OperationOutcome case rather than being skipped as unknown.
        var configuration = Configuration((ValidationKey, "true"));

        var reportValue = configuration
            .GetSection(PreQualificationSettings.Key)
            .Get<PreQualificationSettings>()?.WritePreQualOperationOutcome ?? false;

        Assert.False(reportValue);
        Assert.True(PreQualificationFlagConsistency.TryDetectMismatch(configuration, reportValue, out _));
    }

    [Fact]
    public void ReadsValidationValueFromItsStoredSlashSeparatedKey()
    {
        // Pins the spelling the check depends on. Report sees Validation's App Configuration row under
        // its stored '/'-separated name because the .NET provider passes keys through verbatim; the
        // dotted Spring form is what Validation binds, and is NOT what Report can read.
        var configuration = Configuration(
            (PreQualificationSettings.ValidationServiceKey, "true"));

        Assert.False(PreQualificationFlagConsistency.TryDetectMismatch(configuration, reportValue: false, out _));

        var stored = Configuration(
            (PreQualificationSettings.ValidationServiceAppConfigurationKey, "true"));

        Assert.True(PreQualificationFlagConsistency.TryDetectMismatch(stored, reportValue: false, out _));
    }
}
