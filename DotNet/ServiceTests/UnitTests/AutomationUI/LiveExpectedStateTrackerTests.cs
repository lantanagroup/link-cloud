using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LiveExpectedStateTrackerTests
{
    [Fact]
    public void Admit_without_id_uses_generated_patient_pool()
    {
        var tracker = NewTracker(["pat-1", "pat-2"]);

        var evt = tracker.Admit(null, "Seed", "seeded");

        evt.PatientId.Should().Be("pat-1");
        evt.EventType.Should().Be(PatientEventType.Admit);
        tracker.GetState().Admitted.Should().Equal("pat-1");
        tracker.GetExpectedPopulation().Should().Equal("pat-1");
    }

    [Fact]
    public void Expected_population_is_union_of_admitted_and_discharged()
    {
        var tracker = NewTracker(["pat-1", "pat-2"]);
        tracker.Admit("pat-1", "UI", null);
        tracker.Admit("pat-2", "UI", null);
        tracker.Discharge("pat-1", "UI", null);

        tracker.GetState().Admitted.Should().Equal("pat-2");
        tracker.GetState().DischargedDuringWindow.Should().Equal("pat-1");
        tracker.GetExpectedPopulation().Should().Equal("pat-1", "pat-2");
    }

    [Fact]
    public void Discharge_of_non_admitted_patient_throws()
    {
        var tracker = NewTracker(["pat-1"]);

        var act = () => tracker.Discharge("pat-1", "UI", null);

        act.Should().Throw<LiveInjectionException>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void Freeze_rejects_further_injections()
    {
        var tracker = NewTracker(["pat-1"]);
        tracker.Admit("pat-1", "UI", null);
        tracker.Freeze();

        var act = () => tracker.Admit("pat-2", "UI", null);

        act.Should().Throw<LiveInjectionException>()
            .Which.Message.Should().Contain("not accepting");
        tracker.GetState().AcceptingInjections.Should().BeFalse();
        tracker.GetState().ReportGenerationTimeUtc.Should().NotBeNull();
    }

    [Fact]
    public void Diagnostics_compare_expected_and_actual_populations()
    {
        var tracker = NewTracker(["pat-1", "pat-2"]);
        tracker.Admit("pat-1", "UI", null);
        tracker.Admit("pat-2", "UI", null);
        tracker.Discharge("pat-2", "UI", null);

        var diagnostics = tracker.ToDiagnostics(
            actualPopulation: ["pat-1", "pat-3"],
            inclusionPassed: false,
            missing: ["pat-2"],
            unexpected: ["pat-3"]);

        diagnostics.ExpectedPopulation.Should().Equal("pat-1", "pat-2");
        diagnostics.ActualPopulation.Should().Equal("pat-1", "pat-3");
        diagnostics.MissingFromReport.Should().Equal("pat-2");
        diagnostics.UnexpectedInReport.Should().Equal("pat-3");
        diagnostics.InclusionPassed.Should().BeFalse();
        diagnostics.EventLog.Should().HaveCount(3);
    }

    private static LiveExpectedStateTracker NewTracker(IEnumerable<string> generatedIds)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10), generatedIds);
}
