using Automation.UI.Controllers;
using Automation.UI.Models;
using LantanaGroup.Automation.Generation;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class PatientConfigurationsControllerTests
{
    [Fact]
    public void Stamp_uses_intent_not_a_story_pack()
    {
        var pneumoniaDx = new PatientConfiguration
        {
            Intent = new PatientGenerationIntent
            {
                EncounterClass = "IMP",
                PrimaryConditionSnomed = "233604007",
                IncludeHypoglycemicInsulin = false
            },
            ClinicalScenarioIds = [ClinicalScenarioIds.DiabeticHypoglycemia.ToString()]
        };
        PatientConfigurationsController.StampDerivedQualification(pneumoniaDx);

        Assert.Equal(MeasureEligibility.Qualifying,
            pneumoniaDx.MeasureEligibilities[ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation]);
        Assert.Equal(MeasureEligibility.NonQualifying,
            pneumoniaDx.MeasureEligibilities[ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation]);
    }

    [Fact]
    public void Stamp_insulin_flag_qualifies_hypo_without_pack_id()
    {
        var model = new PatientConfiguration
        {
            Intent = new PatientGenerationIntent
            {
                EncounterClass = "IMP",
                IncludeHypoglycemicInsulin = true
            }
        };
        PatientConfigurationsController.StampDerivedQualification(model);

        Assert.Equal(MeasureEligibility.Qualifying,
            model.MeasureEligibilities[ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation]);
    }
}
