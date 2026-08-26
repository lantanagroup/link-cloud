using Automation.UI.Models;
using Automation.UI.Services;
using LantanaGroup.Link.Automation.Link.Models;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MeasureTemplateRunBinderTests
{
    [Fact]
    public async Task AttachBundles_loads_inline_json_and_derives_families()
    {
        var monthly = new MeasureTemplate
        {
            Id = MeasureTemplateCatalog.AchMonthlyId,
            GenerationFamily = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
            BundleJson = """{"resourceType":"Bundle","id":"monthly"}"""
        };
        var store = new Mock<IMeasureTemplateStore>();
        store.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([monthly]);

        var options = new ResolvedRunOptions(1, 1, 1, 3, 0, 30, false, true,
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation], [], [])
        {
            SelectedMeasureIds = [MeasureTemplateCatalog.AchMonthlyId]
        };

        var bound = await MeasureTemplateRunBinder.AttachBundlesAsync(options, store.Object, CancellationToken.None);

        bound.MeasureBundleJsons.Should().ContainSingle().Which.Should().Contain("monthly");
        bound.SelectedMeasures.Should().Equal(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
    }

    [Fact]
    public async Task AttachBundles_maps_legacy_family_list_to_system_ids()
    {
        var store = new Mock<IMeasureTemplateStore>();
        store.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
            [
                new MeasureTemplate
                {
                    Id = ids.First(),
                    GenerationFamily = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                    BundleJson = "{}"
                }
            ]);

        var options = new ResolvedRunOptions(1, 1, 1, 3, 0, 30, false, true,
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation], [], []);

        var bound = await MeasureTemplateRunBinder.AttachBundlesAsync(options, store.Object, CancellationToken.None);

        bound.SelectedMeasureIds.Should().Equal(MeasureTemplateCatalog.AchMonthlyId);
    }

    [Fact]
    public void ScenarioConfigBuilder_uses_measure_bundle_json()
    {
        var options = new ResolvedRunOptions(1, 1, 1, 3, 0, 30, false, true,
            [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation], [], [])
        {
            MeasureBundleJsons = ["""{"resourceType":"Bundle"}"""]
        };

        var config = ScenarioConfigBuilder.Build(AutomationScenarioKind.Custom, options);

        config.MeasureBundleJsons.Should().ContainSingle();
        config.MeasureBundleLocation.Should().BeEmpty();
        config.AdditionalMeasureBundleLocations.Should().BeEmpty();
    }
}
