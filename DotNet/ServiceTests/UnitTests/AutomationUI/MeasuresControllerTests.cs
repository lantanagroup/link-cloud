using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MeasuresControllerTests
{
    [Fact]
    public async Task SaveInline_rejects_bundle_over_size_cap()
    {
        var store = new Mock<IMeasureTemplateStore>();
        var controller = new MeasuresController(store.Object);
        var model = new MeasureTemplate
        {
            Name = "too-big",
            GenerationFamily = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
            BundleJson = new string('a', MeasuresController.MaxBundleJsonBytes + 1)
        };

        var result = await controller.SaveInline(model, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("1.5", bad.Value?.ToString());
        store.Verify(s => s.UpsertAsync(It.IsAny<MeasureTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
