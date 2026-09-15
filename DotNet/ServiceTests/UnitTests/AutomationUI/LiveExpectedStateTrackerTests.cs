using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Microsoft.AspNetCore.Http;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LiveExpectedStateTrackerTests
{
    [Fact]
    public void Admit_without_id_is_rejected()
    {
        var tracker = NewTracker(["pat-1", "pat-2"]);

        var act = () => tracker.Admit(null, "Seed", "seeded");

        act.Should().Throw<LiveInjectionException>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        tracker.GetState().Admitted.Should().BeEmpty();
    }

    [Fact]
    public void Pool_entry_exposes_data_driven_report_expectation()
    {
        var tracker = NewTracker([
            Seed("remain", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod),
            Seed("outside", ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod)
        ]);

        tracker.GetState().Pool.Should().Contain(p => p.PatientId == "remain" && p.ExpectedInReport);
        tracker.GetState().Pool.Should().Contain(p => p.PatientId == "outside" && !p.ExpectedInReport);
    }

    [Fact]
    public void Census_sets_are_independent_of_report_inclusion()
    {
        var tracker = NewTracker(["pat-1", "pat-2"]);
        tracker.Admit("pat-1", "UI", null);
        tracker.Admit("pat-2", "UI", null);
        tracker.Discharge("pat-1", "UI", null);

        tracker.GetState().Admitted.Should().Equal("pat-2");
        tracker.GetState().DischargedDuringWindow.Should().Equal("pat-1");
        tracker.GetExpectedPopulation().Should().BeEmpty();
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
    public void Diagnostics_compare_data_driven_expected_and_actual_populations()
    {
        var tracker = NewTracker([
            Seed("pat-1", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod),
            Seed("pat-2", ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod)
        ]);
        tracker.Admit("pat-1", "UI", null);
        tracker.Admit("pat-2", "UI", null);
        tracker.Discharge("pat-2", "UI", null);

        var diagnostics = tracker.ToDiagnostics(
            actualPopulation: ["pat-1", "pat-3"],
            inclusionPassed: false,
            missing: ["pat-2"],
            unexpected: ["pat-3"]);

        diagnostics.ExpectedPopulation.Should().Equal("pat-1", "pat-2");
        diagnostics.CurrentlyAdmitted.Should().Equal("pat-1");
        diagnostics.DischargedDuringWindow.Should().Equal("pat-2");
        diagnostics.ActualPopulation.Should().Equal("pat-1", "pat-3");
        diagnostics.MissingFromReport.Should().Equal("pat-2");
        diagnostics.UnexpectedInReport.Should().Equal("pat-3");
        diagnostics.InclusionPassed.Should().BeFalse();
        diagnostics.EventLog.Should().HaveCount(3);
        diagnostics.Pool.Should().HaveCount(2);
        diagnostics.PoolTotals.Total.Should().Be(2);
    }

    [Theory]
    [InlineData(ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod, true, false, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedBeforePeriodDischargedDuringPeriod, true, true, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod, true, false, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod, true, true, true)]
    [InlineData(ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod, false, false, false)]
    [InlineData(ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod, false, false, false)]
    public void Hands_off_pattern_auto_census_matches_GetCensusBehavior(
        ScheduledInpatientPattern pattern,
        bool expectAdmit,
        bool expectDischarge,
        bool expectedInReport)
    {
        var tracker = NewTrackerWithPattern("pat-1", pattern);

        var admits = tracker.ApplyAutomaticAdmits();
        var discharges = tracker.ApplyAutomaticDischarges();

        admits.Should().HaveCount(expectAdmit ? 1 : 0);
        discharges.Should().HaveCount(expectDischarge ? 1 : 0);
        if (expectAdmit)
        {
            admits[0].Source.Should().Be(LiveEventSources.Pattern);
            admits[0].TimestampUtc.Should().Be(tracker.WindowStartUtc);
        }

        if (expectDischarge)
        {
            discharges[0].Source.Should().Be(LiveEventSources.Pattern);
            discharges[0].TimestampUtc.Should().Be(tracker.AutomaticDischargeAtUtc);
        }

        var state = tracker.GetState();
        if (expectedInReport)
            state.ExpectedPopulation.Should().Equal("pat-1");
        else
            state.ExpectedPopulation.Should().BeEmpty();

        var census = state.Pool.Single().CensusState;
        if (expectDischarge)
            census.Should().Be(LivePatientCensusState.DischargedDuringWindow);
        else if (expectAdmit)
            census.Should().Be(LivePatientCensusState.Admitted);
        else
            census.Should().Be(LivePatientCensusState.NotAdmitted);
    }

    [Fact]
    public void Mixed_cohort_hands_off_inclusion_follows_each_pattern()
    {
        var tracker = NewTracker([
            Seed("remain", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod),
            Seed("discharged", ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod),
            Seed("outside", ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod)
        ]);

        tracker.ApplyAutomaticAdmits();
        tracker.ApplyAutomaticDischarges();

        tracker.GetExpectedPopulation().Should().Equal("discharged", "remain");
        tracker.GetState().PoolTotals.Admitted.Should().Be(1);
        tracker.GetState().PoolTotals.DischargedDuringWindow.Should().Be(1);
        tracker.GetState().PoolTotals.NotAdmitted.Should().Be(1);
    }

    [Fact]
    public void Manual_discharge_of_remains_patient_still_expected_in()
    {
        var tracker = NewTrackerWithPattern("pat-1", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);
        tracker.ApplyAutomaticAdmits();

        var evt = tracker.Discharge("pat-1", LiveEventSources.UI, "override");

        evt.Source.Should().Be(LiveEventSources.UI);
        tracker.GetExpectedPopulation().Should().Equal("pat-1");
        tracker.GetState().Pool.Single().CensusState.Should().Be(LivePatientCensusState.DischargedDuringWindow);
    }

    [Fact]
    public void Expected_imported_patient_is_auto_admitted_without_pattern()
    {
        var tracker = NewTracker([
            new LivePatientSeed
            {
                PatientId = "import-q",
                Origin = LivePatientOrigin.Import,
                ExpectedInReport = true
            }
        ]);

        var admits = tracker.ApplyAutomaticAdmits();

        admits.Should().ContainSingle(e => e.PatientId == "import-q");
        tracker.GetExpectedPopulation().Should().Equal("import-q");
        tracker.GetState().Pool.Single().ExpectedInReport.Should().BeTrue();
        tracker.GetState().Pool.Single().CensusState.Should().Be(LivePatientCensusState.Admitted);
    }

    [Fact]
    public void Nq_imported_patient_is_auto_admitted_but_not_expected_in_report()
    {
        var tracker = NewTracker([
            new LivePatientSeed
            {
                PatientId = "import-nq",
                Origin = LivePatientOrigin.Import,
                ExpectedInReport = false
            }
        ]);

        var admits = tracker.ApplyAutomaticAdmits();

        admits.Should().ContainSingle(e => e.PatientId == "import-nq");
        tracker.GetExpectedPopulation().Should().BeEmpty();
        tracker.GetState().Pool.Single().CensusState.Should().Be(LivePatientCensusState.Admitted);
    }

    [Fact]
    public void Manual_admit_of_outside_period_patient_does_not_force_report_inclusion()
    {
        var tracker = NewTrackerWithPattern("pat-1", ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod);
        tracker.ApplyAutomaticAdmits();
        tracker.GetExpectedPopulation().Should().BeEmpty();

        tracker.Admit("pat-1", LiveEventSources.UI, "override");

        tracker.GetState().Admitted.Should().Equal("pat-1");
        tracker.GetExpectedPopulation().Should().BeEmpty();
    }

    [Theory]
    [InlineData(LivePatientOrigin.Generated)]
    [InlineData(LivePatientOrigin.Upload)]
    [InlineData(LivePatientOrigin.FhirId)]
    public void Dynamic_inject_adds_not_admitted_pool_entry(LivePatientOrigin origin)
    {
        var tracker = NewTracker(["cohort-1"]);

        var entry = tracker.AddToPool($"dyn-{origin}", origin);

        entry.CensusState.Should().Be(LivePatientCensusState.NotAdmitted);
        entry.Origin.Should().Be(origin);
        tracker.GetExpectedPopulation().Should().BeEmpty();
        tracker.GetState().PoolTotals.Total.Should().Be(2);
        tracker.GetState().PoolTotals.NotAdmitted.Should().Be(2);
    }

    [Fact]
    public void Automatic_discharge_is_scheduled_at_window_midpoint()
    {
        var start = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var end = start.AddMinutes(10);

        LiveExpectedStateTracker.ComputeAutomaticDischargeAtUtc(start, end)
            .Should().Be(start.AddMinutes(5));
    }

    [Fact]
    public void Re_admit_after_discharge_returns_patient_to_admitted()
    {
        var tracker = NewTracker(["pat-1"]);
        tracker.Admit("pat-1", LiveEventSources.UI, null);
        tracker.Discharge("pat-1", LiveEventSources.UI, null);

        tracker.Admit("pat-1", LiveEventSources.UI, "re-admit");

        tracker.GetState().Admitted.Should().Equal("pat-1");
        tracker.GetState().DischargedDuringWindow.Should().BeEmpty();
        tracker.GetExpectedPopulation().Should().BeEmpty();
        tracker.GetEvents().Should().HaveCount(3);
    }

    [Fact]
    public void Event_log_records_automatic_and_manual_census_with_source_and_timestamp()
    {
        var tracker = NewTrackerWithPattern("pat-1", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);
        tracker.ApplyAutomaticAdmits();
        tracker.Discharge("pat-1", LiveEventSources.UI, "override");

        var events = tracker.GetEvents();
        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(PatientEventType.Admit);
        events[0].Source.Should().Be(LiveEventSources.Pattern);
        events[0].TimestampUtc.Should().Be(tracker.WindowStartUtc);
        events[1].EventType.Should().Be(PatientEventType.Discharge);
        events[1].Source.Should().Be(LiveEventSources.UI);
        events[1].TimestampUtc.Should().NotBe(default);
        tracker.GetExpectedPopulation().Should().Equal("pat-1");
    }

    [Fact]
    public void Mid_window_generate_does_not_expect_report_until_admitted()
    {
        var tracker = NewTracker(["cohort-1"]);

        var entry = tracker.AddToPool("live-gen-1", LivePatientOrigin.Generated, expectedInReport: true);

        entry.CensusState.Should().Be(LivePatientCensusState.NotAdmitted);
        entry.ExpectedInReport.Should().BeTrue();
        tracker.GetState().Admitted.Should().BeEmpty();
        tracker.GetExpectedPopulation().Should().BeEmpty();
        var inject = tracker.GetEvents().Should().ContainSingle(e => e.EventType == PatientEventType.Inject).Subject;
        inject.PatientId.Should().Be("live-gen-1");
        inject.TimestampUtc.Should().NotBe(default);

        tracker.Admit("live-gen-1", LiveEventSources.UI, null);

        tracker.GetExpectedPopulation().Should().Equal("live-gen-1");
        tracker.GetState().Admitted.Should().Equal("live-gen-1");
    }

    [Fact]
    public void Mid_window_generate_non_qualifying_is_not_expected_after_admit()
    {
        var tracker = NewTracker(["cohort-1"]);
        tracker.AddToPool("live-nq", LivePatientOrigin.Generated, expectedInReport: false);

        tracker.Admit("live-nq", LiveEventSources.UI, null);

        tracker.GetState().Admitted.Should().Equal("live-nq");
        tracker.GetExpectedPopulation().Should().BeEmpty();
    }

    [Fact]
    public void Admit_unknown_patient_when_pool_exists_is_rejected()
    {
        var tracker = NewTracker(["pat-1"]);

        var act = () => tracker.Admit("unknown", LiveEventSources.UI, null);

        act.Should().Throw<LiveInjectionException>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    private static LiveExpectedStateTracker NewTracker(IEnumerable<string> generatedIds)
        => new(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10), generatedIds);

    private static LiveExpectedStateTracker NewTracker(IEnumerable<LivePatientSeed> seeds)
    {
        var start = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        return new LiveExpectedStateTracker(Guid.NewGuid(), start, start.AddMinutes(10), seeds);
    }

    private static LiveExpectedStateTracker NewTrackerWithPattern(string patientId, ScheduledInpatientPattern pattern)
        => NewTracker([Seed(patientId, pattern)]);

    private static LivePatientSeed Seed(string patientId, ScheduledInpatientPattern pattern)
        => new()
        {
            PatientId = patientId,
            Origin = LivePatientOrigin.Cohort,
            Pattern = pattern
        };
}
