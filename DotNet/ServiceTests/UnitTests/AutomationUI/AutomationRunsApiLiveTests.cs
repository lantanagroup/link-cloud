using Automation.UI.Controllers.Api;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AutomationRunsApiLiveTests
{
    [Fact]
    public async Task Patient_state_includes_pool_after_open()
    {
        var runId = Guid.NewGuid();
        var manager = new Mock<IAutomationRunManager>();
        manager.Setup(m => m.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRunSummary { RunId = runId, Status = AutomationRunStatus.LiveWindowOpen });
        manager.Setup(m => m.GetLivePatientStateAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LivePatientStateSnapshot
            {
                Admitted = ["pat-1"],
                ExpectedPopulation = ["pat-1"],
                AcceptingInjections = true,
                Pool =
                [
                    new LivePatientPoolEntry
                    {
                        PatientId = "pat-1",
                        Origin = LivePatientOrigin.Cohort,
                        CensusState = LivePatientCensusState.Admitted
                    }
                ],
                PoolTotals = new LivePatientPoolTotals { Total = 1, Admitted = 1 }
            });

        var result = await CreateController(manager).GetPatientState(runId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("pat-1");
        json.Should().Contain("pool");
    }

    [Theory]
    [InlineData("admit")]
    [InlineData("discharge")]
    [InlineData("generate")]
    [InlineData("upload")]
    [InlineData("reference")]
    public async Task Inject_when_window_closed_returns_409(string op)
    {
        var runId = Guid.NewGuid();
        var manager = new Mock<IAutomationRunManager>();
        manager.Setup(m => m.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRunSummary { RunId = runId, Status = AutomationRunStatus.Running });
        var closed = new LiveInjectionException("Live window is not accepting injections.", StatusCodes.Status409Conflict);
        manager.Setup(m => m.InjectAdmitAsync(runId, It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(closed);
        manager.Setup(m => m.InjectDischargeAsync(runId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(closed);
        manager.Setup(m => m.GenerateLivePoolPatientAsync(runId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(closed);
        manager.Setup(m => m.UploadLivePoolPatientAsync(runId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(closed);
        manager.Setup(m => m.ReferenceLivePoolPatientAsync(runId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(closed);

        var controller = CreateController(manager);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { Request = { Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}")) } }
        };

        IActionResult result = op switch
        {
            "admit" => await controller.Admit(runId, new AutomationRunsApiController.LivePatientEventRequest { PatientId = "pat-1" }, CancellationToken.None),
            "discharge" => await controller.Discharge(runId, new AutomationRunsApiController.LivePatientEventRequest { PatientId = "pat-1" }, CancellationToken.None),
            "generate" => await controller.GeneratePoolPatient(runId, CancellationToken.None),
            "upload" => await controller.UploadPoolPatient(runId, CancellationToken.None),
            _ => await controller.ReferencePoolPatient(runId, new AutomationRunsApiController.LivePoolReferenceRequest { PatientId = "pat-1" }, CancellationToken.None)
        };

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void GetCensusBehavior_contract_is_unchanged()
    {
        ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod.GetCensusBehavior()
            .Should().Be(new ScheduledPatternCensusBehavior(true, false, true));
        ScheduledInpatientPattern.AdmittedBeforePeriodDischargedDuringPeriod.GetCensusBehavior()
            .Should().Be(new ScheduledPatternCensusBehavior(true, true, true));
        ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod.GetCensusBehavior()
            .Should().Be(new ScheduledPatternCensusBehavior(true, false, true));
        ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod.GetCensusBehavior()
            .Should().Be(new ScheduledPatternCensusBehavior(true, true, true));
        ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod.GetCensusBehavior()
            .Should().Be(new ScheduledPatternCensusBehavior(false, false, false));
        ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod.GetCensusBehavior()
            .Should().Be(new ScheduledPatternCensusBehavior(false, false, false));
    }

    private static AutomationRunsApiController CreateController(Mock<IAutomationRunManager> manager)
        => new(
            manager.Object,
            Mock.Of<IScenarioStore>(),
            NullLogger<AutomationRunsApiController>.Instance);
}
