using LantanaGroup.Automation.Generation;

namespace Automation.UI.Models;

/// <summary>
/// Lightweight projection of a clinical scenario for display in the UI.
/// </summary>
public class ClinicalScenarioInfo
{
    public string ScenarioId { get; init; } = string.Empty;
    public string PrimaryDiagnosis { get; init; } = string.Empty;
    public string IcdCode { get; init; } = string.Empty;
    public string AdmitType { get; init; } = string.Empty;
    public string ServiceType { get; init; } = string.Empty;

    /// <summary>
    /// Which measures this scenario's qualifying patients are expected to pass.
    /// </summary>
    public List<string> QualifiesForMeasures { get; init; } = [];

    /// <summary>
    /// Which measures this scenario's patients are expected to fail.
    /// </summary>
    public List<string> FailsForMeasures { get; init; } = [];

    public List<string> QualifiesForMeasureTypes { get; init; } = [];
    public List<string> FailsForMeasureTypes { get; init; } = [];

    /// <summary>
    /// Builds info items for all clinical scenarios.
    /// </summary>
    public static List<ClinicalScenarioInfo> GetAll(IReadOnlyList<ProfiledMeasureType> selectedMeasures)
    {
        var scenarios = FhirGenerationCodes.ClinicalScenarios;
        var result = new List<ClinicalScenarioInfo>(scenarios.Length);

        foreach (var s in scenarios)
        {
            var qualifies = new List<string>();
            var fails = new List<string>();
            var qualifiesByType = new List<string>();
            var failsByType = new List<string>();

            foreach (var measure in selectedMeasures)
            {
                var displayName = ProfiledMeasureCatalog.GetDisplayName(measure);
                if (ClinicalScenarioEligibility.QualifiesForMeasure(s, measure))
                {
                    qualifies.Add(displayName);
                    qualifiesByType.Add(measure.ToString());
                }
                else
                {
                    fails.Add(displayName);
                    failsByType.Add(measure.ToString());
                }
            }

            result.Add(new ClinicalScenarioInfo
            {
                ScenarioId = s.ScenarioId.ToString(),
                PrimaryDiagnosis = s.PrimaryDxDisplay,
                IcdCode = s.PrimaryDxIcd,
                AdmitType = s.AdmitTypeDisplay,
                ServiceType = s.ServiceTypeDisplay,
                QualifiesForMeasures = qualifies,
                FailsForMeasures = fails,
                QualifiesForMeasureTypes = qualifiesByType,
                FailsForMeasureTypes = failsByType
            });
        }

        return result;
    }
}
