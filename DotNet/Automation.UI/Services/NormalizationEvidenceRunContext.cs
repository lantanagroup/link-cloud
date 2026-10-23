using LantanaGroup.Automation.Generation;
using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// Copies patient-source facts onto a normalization evidence snapshot before it is stored.
/// </summary>
internal static class NormalizationEvidenceRunContext
{
    public static void Apply(
        NormalizationEvidenceSnapshot evidence,
        GenerationManifest? manifest,
        bool generatorAppliedSelectedSuite)
    {
        if (manifest != null)
        {
            var origins = PatientOrigin.Resolve(
                manifest.PatientIds,
                manifest.PreExistingPatientIds,
                manifest.TemplateCacheKeyByPatient);
            evidence.GeneratedPatientCount = origins.Values.Count(value => value == PatientOrigin.Generated);
            evidence.UploadedPatientCount = origins.Values.Count(value => value == PatientOrigin.Uploaded);
        }

        evidence.GeneratorAppliedSelectedSuite = generatorAppliedSelectedSuite && evidence.GeneratedPatientCount > 0;
        evidence.EvidenceCollected = true;
        if (evidence.EvidenceInventory == null || evidence.EvidenceInventory.Count == 0)
            evidence.EvidenceInventory = NormalizationDiagnosticsWriter.FormatEvidenceInventory(evidence.ParsedSteps).ToList();
    }
}
