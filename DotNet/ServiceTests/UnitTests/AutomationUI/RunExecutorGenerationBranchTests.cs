using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Moq;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunExecutorGenerationBranchTests
{
    [Fact]
    public void CreateAcquisitionSimulationConfig_builds_expected_shared_config()
    {
        var queryPlan = new QueryPlanInput();
        var template = new OrganizationResourceMapTemplate
        {
            Conditions =
            [
                new OrganizationResourceMapCondition { FhirPath = "Patient.managingOrganization", Priority = 2 },
                new OrganizationResourceMapCondition { FhirPath = "Encounter.serviceProvider", Priority = 1 },
                new OrganizationResourceMapCondition { FhirPath = "  ", Priority = 3 }
            ]
        };

        var config = RunExecutor.CreateAcquisitionSimulationConfig(
            queryPlan,
            "2023-01-01T00:00:00Z",
            "2024-01-01T00:00:00Z",
            template);

        config.QueryPlan.Should().BeSameAs(queryPlan);
        config.ClinicalPeriodStart.Should().Be("2023-01-01T00:00:00Z");
        config.ClinicalPeriodEnd.Should().Be("2024-01-01T00:00:00Z");
        config.AllowEncounterAnchoredDateOverrideForOutOfRange.Should().BeFalse();
        config.OrganizationLocationConditionFhirPaths.Should().NotBeNull();
        config.OrganizationLocationConditionFhirPaths!.Should().HaveCount(2);
        config.OrganizationLocationConditionFhirPaths[0].Should().Contain("Encounter");
        config.OrganizationLocationConditionFhirPaths[1].Should().Contain("Patient");
    }

    [Fact]
    public void BuildProfileGenerationRequest_preserves_profile_branch_inputs()
    {
        var selectedMeasures = new List<ProfiledMeasureType>
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
        };

        var profiles = new List<PatientProfile>
        {
            new(new Dictionary<ProfiledMeasureType, MeasureEligibility>
            {
                [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
            })
        };

        var importedPatients = new List<ImportedPatientInput>
        {
            new() { PatientId = "Patient-001", Source = ImportedPatientSource.ExistingId }
        };

        var cache = new Mock<IGeneratedPatientTemplateCache>(MockBehavior.Strict).Object;

        var request = RunExecutor.BuildProfileGenerationRequest(selectedMeasures, profiles, importedPatients, cache);

        request.SelectedMeasures.Should().BeSameAs(selectedMeasures);
        request.Profiles.Should().BeSameAs(profiles);
        request.ImportedPatients.Should().BeSameAs(importedPatients);
        request.GeneratedTemplateCache.Should().BeSameAs(cache);
    }

    [Fact]
    public void BuildNonProfileGenerationRequest_creates_all_qualifying_synthetic_profiles()
    {
        var selectedMeasures = new List<ProfiledMeasureType>
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
        };

        var request = RunExecutor.BuildNonProfileGenerationRequest(
            selectedMeasures,
            patientCount: 4,
            resourcesPerPatient: 123,
            seed: 20260812);

        request.SelectedMeasures.Should().BeSameAs(selectedMeasures);
        request.ImportedPatients.Should().BeNull();
        request.GeneratedTemplateCache.Should().BeNull();
        request.Profiles.Should().HaveCount(4);
        request.Profiles.Should().OnlyContain(profile =>
            selectedMeasures.All(profile.QualifiesFor)
            && profile.ResourcesPerPatient == 123);
    }
}
