using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class FacilityTemplateTests
{
    [Fact]
    public void Seeded_templates_keep_system_behavior_and_split_epic_from_cerner()
    {
        var templates = FacilityTemplateSeedService.BuildTemplates();

        var system = templates.Single(t => t.Id == FacilityTemplateCatalog.SystemDefaultId);
        system.IsDefault.Should().BeTrue();
        system.VendorName.Should().Be("Epic");
        system.QueryPlanTemplateId.Should().Be(FacilityTemplateCatalog.SystemQueryPlanId);
        system.NormalizationSuiteId.Should().Be(FacilityTemplateCatalog.SystemNormalizationSuiteId);
        system.OrganizationResourceMapTemplateId.Should().Be(FacilityTemplateCatalog.SystemOrganizationResourceMapId);
        system.EnableOrganizationLocationMapping.Should().BeTrue();
        system.AllowPatientConfigurationsOutsideSet.Should().BeTrue();

        var epic = templates.Single(t => t.Id == FacilityTemplateCatalog.EpicId);
        var cerner = templates.Single(t => t.Id == FacilityTemplateCatalog.CernerId);

        epic.VendorName.Should().Be("Epic");
        cerner.VendorName.Should().Be("Cerner");
        epic.QueryPlanTemplateId.Should().Be(FacilityTemplateCatalog.EpicQueryPlanId);
        cerner.QueryPlanTemplateId.Should().Be(FacilityTemplateCatalog.CernerQueryPlanId);
        epic.NormalizationSuiteId.Should().NotBeNull();
        cerner.NormalizationSuiteId.Should().NotBeNull();
        epic.NormalizationSuiteId!.Value.Should().NotBe(cerner.NormalizationSuiteId!.Value);
        epic.OrganizationResourceMapTemplateId.Should().NotBeNull();
        cerner.OrganizationResourceMapTemplateId.Should().NotBeNull();
        epic.OrganizationResourceMapTemplateId!.Value.Should().NotBe(cerner.OrganizationResourceMapTemplateId!.Value);
        epic.EnableOrganizationLocationMapping.Should().BeTrue();
        cerner.EnableOrganizationLocationMapping.Should().BeTrue();

        FacilityTemplateCatalog.EpicOrgLocationFhirPath.Should().Contain(FacilityTemplateCatalog.EpicLocationIdentifierSystem);
        FacilityTemplateCatalog.EpicOrgLocationFhirPath.Should().Contain("HOSP");
        FacilityTemplateCatalog.CernerOrgLocationFhirPath.Should().Contain(FacilityTemplateCatalog.CernerLocationTypeSystem);
        FacilityTemplateCatalog.CernerOrgLocationFhirPath.Should().NotContain(FacilityTemplateCatalog.EpicLocationIdentifierSystem);
        FacilityTemplateCatalog.EpicOrgLocationFhirPath.Should().NotContain(FacilityTemplateCatalog.CernerLocationTypeSystem);
    }

    [Fact]
    public void Closed_patient_set_rejects_a_configuration_outside_it()
    {
        var allowed = Guid.NewGuid();
        var template = new FacilityTemplate
        {
            Name = "Closed",
            AllowPatientConfigurationsOutsideSet = false,
            AllowedPatientConfigurationIds = [allowed]
        };

        FacilityConfigurationPolicy.ValidatePatientConfigurations(template,
            [new PatientCohortDefinition { PatientConfigurationId = allowed }])
            .Should().BeNull();

        FacilityConfigurationPolicy.ValidatePatientConfigurations(template,
            [new PatientCohortDefinition { PatientConfigurationId = Guid.NewGuid() }])
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Open_patient_set_accepts_configurations_outside_it()
    {
        var template = new FacilityTemplate
        {
            Name = "Open",
            AllowPatientConfigurationsOutsideSet = true,
            AllowedPatientConfigurationIds = [Guid.NewGuid()]
        };

        FacilityConfigurationPolicy.ValidatePatientConfigurations(template,
            [new PatientCohortDefinition { PatientConfigurationId = Guid.NewGuid() }])
            .Should().BeNull();
    }

    [Fact]
    public async Task Binder_copies_the_template_and_does_not_keep_ala_carte_ids()
    {
        var template = FacilityTemplateSeedService.BuildTemplates().Single(t => t.Id == FacilityTemplateCatalog.CernerId);
        var store = new FakeFacilityTemplateStore(template);
        var options = new ResolvedRunOptions(1, 10, 1, 1, 1, 1, false, true, [], [], [])
        {
            FacilityConfigurationMode = FacilityConfigurationMode.Facility,
            FacilityTemplateId = template.Id,
            QueryPlanTemplateId = Guid.NewGuid(),
            NormalizationSuiteId = Guid.NewGuid(),
            OrganizationResourceMapTemplateId = Guid.NewGuid(),
            VendorName = "Epic"
        };

        var bound = await FacilityTemplateRunBinder.ApplyAsync(options, store);

        bound.QueryPlanTemplateId.Should().Be(template.QueryPlanTemplateId);
        bound.NormalizationSuiteId.Should().Be(template.NormalizationSuiteId);
        bound.OrganizationResourceMapTemplateId.Should().Be(template.OrganizationResourceMapTemplateId);
        bound.VendorName.Should().Be("Cerner");
        bound.HonorExplicitFacilityPieces.Should().BeTrue();
    }

    [Fact]
    public async Task Binder_turns_org_mapping_off_when_the_template_says_so()
    {
        var template = new FacilityTemplate
        {
            Id = Guid.NewGuid(),
            Name = "No org",
            EnableOrganizationLocationMapping = false,
            OrganizationResourceMapTemplateId = Guid.NewGuid(),
            AllowPatientConfigurationsOutsideSet = true
        };
        var options = new ResolvedRunOptions(1, 10, 1, 1, 1, 1, false, true, [], [], [])
        {
            FacilityConfigurationMode = FacilityConfigurationMode.Facility,
            FacilityTemplateId = template.Id
        };

        var bound = await FacilityTemplateRunBinder.ApplyAsync(options, new FakeFacilityTemplateStore(template));

        bound.OrganizationResourceMapTemplateId.Should().BeNull();
    }

    private sealed class FakeFacilityTemplateStore(FacilityTemplate template) : IFacilityTemplateStore
    {
        public Task<List<FacilityTemplate>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<FacilityTemplate> { template });

        public Task<FacilityTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == template.Id ? template : null);

        public Task<FacilityTemplate?> GetDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult<FacilityTemplate?>(template.IsDefault ? template : null);

        public Task UpsertAsync(FacilityTemplate template, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetDefaultAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
