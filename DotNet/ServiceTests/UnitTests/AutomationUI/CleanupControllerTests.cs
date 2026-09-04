using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class CleanupControllerTests
{
    [Fact]
    public async Task SaveSettings_WithoutRunKind_SavesAndDoesNotStartAPass()
    {
        var cleanup = MockCleanup();
        var store = MockStore();
        var sut = Create(cleanup, store);

        var result = await sut.SaveSettings(ValidForm(), runKind: "", CancellationToken.None);

        store.Verify(s => s.SaveAsync(It.IsAny<LeftoverRunCleanupSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        cleanup.Verify(c => c.StartQuiesceInBackground(), Times.Never);
        cleanup.Verify(c => c.StartTeardownInBackground(), Times.Never);
        cleanup.Verify(c => c.StartHistoryPurgeInBackground(), Times.Never);
        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public async Task SaveSettings_WithQuiesceRunKind_SavesThenStartsBackgroundPass()
    {
        var cleanup = MockCleanup();
        var store = MockStore();
        var sut = Create(cleanup, store);

        var result = await sut.SaveSettings(ValidForm(), "quiesce", CancellationToken.None);

        store.Verify(s => s.SaveAsync(It.IsAny<LeftoverRunCleanupSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        cleanup.Verify(c => c.StartQuiesceInBackground(), Times.Once);
        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveSettings_WhenAlreadyRunning_DoesNotStartAnotherPass()
    {
        var cleanup = MockCleanup();
        cleanup.SetupGet(c => c.IsRunning).Returns(true);
        var sut = Create(cleanup, MockStore());

        var result = await sut.SaveSettings(ValidForm(), "teardown", CancellationToken.None);

        cleanup.Verify(c => c.StartTeardownInBackground(), Times.Never);
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public void ApplyForm_ClampsRetentionAndInterval()
    {
        var current = new LeftoverRunCleanupSettings();
        var settings = CleanupController.ApplyForm(
            new CleanupSettingsForm
            {
                TeardownRetentionDays = 9999,
                QuiesceIntervalMinutes = 0,
                AbortTtlDays = 0,
                MaxFacilitiesPerPass = 0,
                CatchUpWindowHours = 99,
                DailyTeardownTimeUtc = "10:00",
                WeeklyHistoryPurgeTimeUtc = "10:00"
            },
            current);

        settings.TeardownRetention.Should().Be(TimeSpan.FromDays(365));
        settings.QuiesceInterval.Should().Be(TimeSpan.FromMinutes(1));
        settings.AbortTtl.Should().Be(TimeSpan.FromDays(1));
        settings.MaxFacilitiesPerPass.Should().Be(1);
        settings.CatchUpWindow.Should().Be(TimeSpan.FromHours(12));
    }

    [Fact]
    public void CleanupActivity_Percent_IsZeroWhenIdle()
        => CleanupActivity.Idle.Percent.Should().Be(0);

    [Fact]
    public void CleanupActivity_Percent_UsesProcessedOverTotal()
        => new CleanupActivity
        {
            Mode = "teardown",
            Label = "Off-hours leftover teardown",
            Status = "running",
            Trigger = "manual",
            Total = 4,
            Processed = 1,
            At = DateTimeOffset.UtcNow
        }.Percent.Should().Be(25);

    private static CleanupController Create(Mock<ILeftoverRunCleanup> cleanup, Mock<ICleanupSettingsStore> store)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Accept = "application/json";
        var sut = new CleanupController(
            cleanup.Object,
            store.Object,
            TimeProvider.System,
            Mock.Of<ILogger<CleanupController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
        return sut;
    }

    private static Mock<ILeftoverRunCleanup> MockCleanup()
    {
        var cleanup = new Mock<ILeftoverRunCleanup>();
        cleanup.SetupGet(c => c.IsRunning).Returns(false);
        cleanup.SetupGet(c => c.CurrentActivity).Returns(CleanupActivity.Idle);
        return cleanup;
    }

    private static Mock<ICleanupSettingsStore> MockStore()
    {
        var store = new Mock<ICleanupSettingsStore>();
        store.Setup(s => s.GetEffectiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeftoverRunCleanupSettings());
        store.Setup(s => s.SaveAsync(It.IsAny<LeftoverRunCleanupSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return store;
    }

    private static CleanupSettingsForm ValidForm()
        => new()
        {
            Enabled = true,
            QuiesceEnabled = true,
            QuiesceIntervalMinutes = 5,
            QuiesceGraceMinutes = 2,
            TeardownRetentionDays = 14,
            AbortTtlDays = 14,
            MaxFacilitiesPerPass = 25,
            DailyTeardownEnabled = true,
            DailyTeardownTimeUtc = "10:00",
            WeeklyHistoryPurgeEnabled = true,
            WeeklyHistoryPurgeDay = DayOfWeek.Sunday,
            WeeklyHistoryPurgeTimeUtc = "10:00",
            CatchUpWindowHours = 3
        };
}
