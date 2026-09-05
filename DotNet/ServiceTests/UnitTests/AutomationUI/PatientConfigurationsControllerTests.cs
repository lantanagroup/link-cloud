using Automation.UI.Controllers;
using LantanaGroup.Automation.Generation;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class PatientConfigurationsControllerTests
{
    [Fact]
    public void Sanitize_keeps_a_valid_clinical_scenario_id()
    {
        var id = ClinicalScenarioIds.Pneumonia.ToString();
        var kept = PatientConfigurationsController.SanitizeClinicalScenarioIds([id, "not-a-guid"], null);
        Assert.Equal(id, Assert.Single(kept));
    }

    [Fact]
    public void Sanitize_allows_empty_when_no_matching_primary_dx()
    {
        var kept = PatientConfigurationsController.SanitizeClinicalScenarioIds(
            ["nope"],
            new PatientGenerationIntent { PrimaryConditionSnomed = "999999999" });
        Assert.Empty(kept);
    }

    [Fact]
    public void Sanitize_stamps_engine_pack_from_primary_dx_when_ids_missing()
    {
        var kept = PatientConfigurationsController.SanitizeClinicalScenarioIds(
            [],
            new PatientGenerationIntent { PrimaryConditionSnomed = "233604007" });
        Assert.Equal(ClinicalScenarioIds.Pneumonia.ToString(), Assert.Single(kept));
    }
}
