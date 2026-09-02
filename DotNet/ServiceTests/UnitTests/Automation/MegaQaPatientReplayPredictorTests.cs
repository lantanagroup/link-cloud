using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

/// <summary>
/// Opt-in replay of the QA mega-patient bundles from Downloads through
/// <see cref="AbsSubmissionPredictor"/>. Gold is the Run Diagnostics ABS actuals.
/// Set MEGA_QA_REPLAY=1 to run; skipped in CI and default local test runs.
/// </summary>
[Trait("Category", "UnitTests")]
public class MegaQaPatientReplayPredictorTests
{
    private static readonly string MegaRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "20260901_ACH_3_14hrk4jaItgGPfBsIB16onywhnu3fSqRLEy32IXJaYNkK");

    private static readonly DateTime PeriodStart = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

    // Condition actually saved on the generated ORM for the QA mega e2e
    // (f4f04f73-a9a2-4201-b5eb-96f508b0b223). Only HSLOC 1039-7 hospital
    // roots (and their partOf children) are in-org.
    private static readonly string[] MegaOrmFhirPaths =
    [
        "Location.type.coding.where(system = 'https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html' and code = '1039-7').exists()"
    ];

    [Fact]
    public void V9v_mega_patient_predictor_matches_abs_diagnostic_report_and_medreq()
    {
        const string patientId = "v9vVDGVXbHjGRe6K7xb2iwn6a8JF3qCjz1vKaQOCzllKy";
        var bundlePath = Path.Combine(
            MegaRoot,
            "20260901_ACH_1_v9vVDGVXbHjGRe6K7xb2iwn6a8JF3qCjz1vKaQOCzllKy.json");
        if (!ShouldReplay(bundlePath))
            return;

        var predicted = Predict(bundlePath, patientId);
        predicted["DiagnosticReport"].Should().Be(259, "ABS actual DiagnosticReport count");
        predicted["MedicationRequest"].Should().Be(15, "ABS actual MedicationRequest count");
        predicted["Encounter"].Should().Be(2, "ABS actual Encounter count");
    }

    [Fact]
    public void Mega14hrk_patient_predictor_matches_abs_diagnostic_report()
    {
        const string patientId = "14hrk4jaItgGPfBsIB16onywhnu3fSqRLEy32IXJaYNkK";
        var bundlePath = Path.Combine(
            MegaRoot,
            "20260901_ACH_3_14hrk4jaItgGPfBsIB16onywhnu3fSqRLEy32IXJaYNkK.json");
        if (!ShouldReplay(bundlePath))
            return;

        var predicted = Predict(bundlePath, patientId);
        predicted["DiagnosticReport"].Should().Be(326, "ABS actual DiagnosticReport count");
        predicted["MedicationRequest"].Should().Be(11, "ABS actual MedicationRequest count");
        predicted["Encounter"].Should().Be(1, "ABS actual Encounter count");
    }

    private static bool ShouldReplay(string bundlePath)
        => File.Exists(bundlePath)
           && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MEGA_QA_REPLAY"));

    private static Dictionary<string, int> Predict(string bundlePath, string patientId)
    {
        var manifest = AbsSubmissionPredictor.PredictImportedBundle(
            File.ReadAllText(bundlePath),
            patientId,
            PeriodStart,
            PeriodEnd,
            organizationLocationConditionFhirPaths: MegaOrmFhirPaths);
        return manifest.GetExpectedAbsCountsForPatient(patientId)
               ?? throw new InvalidOperationException(patientId);
    }
}
