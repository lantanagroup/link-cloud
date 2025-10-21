using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LivePatientEventInjectorTests
{
    [Fact]
    public async Task OpenSession_applies_no_auto_census_until_explicitly_requested()
    {
        var injector = CreateInjector();
        var runId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

        injector.OpenSession(
            runId,
            start,
            start.AddMinutes(10),
            poolSeeds:
            [
                new LivePatientSeed
                {
                    PatientId = "pat-remain",
                    Origin = LivePatientOrigin.Cohort,
                    Pattern = ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod
                }
            ]);

        injector.GetState(runId).Admitted.Should().BeEmpty();

        var admits = await injector.ApplyAutomaticAdmitsAsync(runId);
        admits.Should().ContainSingle(e => e.PatientId == "pat-remain" && e.Source == LiveEventSources.Pattern);
        injector.GetState(runId).ExpectedPopulation.Should().Equal("pat-remain");
    }

    [Fact]
    public async Task Hands_off_mixed_patterns_match_inclusion_rule()
    {
        var injector = CreateInjector();
        var runId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

        injector.OpenSession(
            runId,
            start,
            start.AddMinutes(2),
            poolSeeds:
            [
                Seed("remain", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod),
                Seed("discharged", ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod),
                Seed("outside", ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod)
            ]);

        await injector.ApplyAutomaticAdmitsAsync(runId);
        await injector.ApplyAutomaticDischargesAsync(runId);
        await injector.FreezeAsync(runId);

        var state = injector.GetState(runId);
        state.ExpectedPopulation.Should().Equal("discharged", "remain");
        state.AcceptingInjections.Should().BeFalse();
        state.PoolTotals.Total.Should().Be(3);
    }

    [Fact]
    public async Task Generate_upload_and_reference_add_not_admitted_pool_entries()
    {
        var injector = CreateInjector();
        var runId = Guid.NewGuid();
        injector.OpenSession(runId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), generatedPatientIds: ["cohort-1"]);

        var generated = await injector.GeneratePoolPatientAsync(runId);
        var uploaded = await injector.UploadPoolPatientAsync(runId, """{"resourceType":"Patient","id":"upload-1"}""");
        var referenced = await injector.ReferencePoolPatientAsync(runId, "fhir-99");

        generated.Origin.Should().Be(LivePatientOrigin.Generated);
        generated.CensusState.Should().Be(LivePatientCensusState.NotAdmitted);
        uploaded.PatientId.Should().Be("upload-1");
        uploaded.Origin.Should().Be(LivePatientOrigin.Upload);
        referenced.PatientId.Should().Be("fhir-99");
        referenced.Origin.Should().Be(LivePatientOrigin.FhirId);

        var state = injector.GetState(runId);
        state.PoolTotals.Total.Should().Be(4);
        state.PoolTotals.NotAdmitted.Should().Be(4);
        state.ExpectedPopulation.Should().BeEmpty();

        var injects = injector.GetEvents(runId).Where(e => e.EventType == PatientEventType.Inject).ToList();
        injects.Should().HaveCount(3);
        injects.Select(e => e.Source).Should().Equal(LiveEventSources.Generated, LiveEventSources.Upload, LiveEventSources.FhirId);
        injects.Should().OnlyContain(e => e.TimestampUtc != default);
    }

    [Fact]
    public async Task Pool_ops_after_close_return_409()
    {
        var injector = CreateInjector();
        var runId = Guid.NewGuid();
        injector.OpenSession(runId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));
        await injector.FreezeAsync(runId);

        var act = async () => await injector.GeneratePoolPatientAsync(runId);
        var ex = await act.Should().ThrowAsync<LiveInjectionException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Missing_session_returns_409()
    {
        var injector = CreateInjector();

        var act = async () => await injector.AdmitAsync(Guid.NewGuid(), "pat-1", LiveEventSources.API);
        var ex = await act.Should().ThrowAsync<LiveInjectionException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Generate_upload_and_reference_use_provisioner_and_data_driven_expectation()
    {
        var provisioner = new RecordingProvisioner();
        var injector = CreateInjector(provisioner);
        var runId = Guid.NewGuid();
        injector.OpenSession(
            runId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            generatedPatientIds: ["cohort-1"],
            patientProvisioner: provisioner);

        var generated = await injector.GeneratePoolPatientAsync(runId);
        var uploaded = await injector.UploadPoolPatientAsync(runId, """{"resourceType":"Patient","id":"upload-1"}""");
        var referenced = await injector.ReferencePoolPatientAsync(runId, "fhir-99");

        generated.PatientId.Should().Be("gen-appended");
        uploaded.PatientId.Should().Be("upload-appended");
        referenced.PatientId.Should().Be("ref-appended");
        provisioner.GenerateCalls.Should().Be(1);
        provisioner.UploadCalls.Should().Be(1);
        provisioner.ReferenceCalls.Should().Be(1);

        var state = injector.GetState(runId);
        state.ExpectedPopulation.Should().BeEmpty();
        state.Admitted.Should().BeEmpty();
        state.Pool.Should().Contain(p => p.PatientId == "gen-appended" && p.ExpectedInReport);
        state.Pool.Should().Contain(p => p.PatientId == "upload-appended" && p.ExpectedInReport);
        state.Pool.Should().Contain(p => p.PatientId == "ref-appended" && !p.ExpectedInReport);
        injector.GetEvents(runId).Where(e => e.EventType == PatientEventType.Inject)
            .Select(e => e.PatientId).Should().Equal("gen-appended", "upload-appended", "ref-appended");

        await injector.AdmitAsync(runId, "gen-appended", LiveEventSources.UI);
        injector.GetState(runId).ExpectedPopulation.Should().Equal("gen-appended");
    }

    [Fact]
    public async Task RecordActualPopulation_uses_caller_expected_not_census_union()
    {
        var injector = CreateInjector();
        var runId = Guid.NewGuid();
        injector.OpenSession(
            runId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            poolSeeds:
            [
                Seed("remain", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod),
                Seed("outside", ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod)
            ]);

        await injector.AdmitAsync(runId, "outside", LiveEventSources.UI);
        await injector.DischargeAsync(runId, "outside", LiveEventSources.UI);

        await injector.RecordActualPopulationAsync(
            runId,
            actualPopulation: ["remain"],
            expectedPopulation: ["remain"]);

        var diagnostics = injector.GetDiagnostics(runId);
        diagnostics.ExpectedPopulation.Should().Equal("remain");
        diagnostics.CurrentlyAdmitted.Should().BeEmpty();
        diagnostics.DischargedDuringWindow.Should().Equal("outside");
        diagnostics.InclusionPassed.Should().BeTrue();
        diagnostics.MissingFromReport.Should().BeEmpty();
    }

    [Fact]
    public async Task Admit_and_discharge_do_not_invoke_provisioner()
    {
        var provisioner = new RecordingProvisioner();
        var injector = CreateInjector(provisioner);
        var runId = Guid.NewGuid();
        injector.OpenSession(
            runId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            poolSeeds:
            [
                Seed("remain", ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod)
            ],
            patientProvisioner: provisioner);

        await injector.AdmitAsync(runId, "remain", LiveEventSources.UI);
        await injector.DischargeAsync(runId, "remain", LiveEventSources.UI);

        provisioner.GenerateCalls.Should().Be(0);
        provisioner.UploadCalls.Should().Be(0);
        provisioner.ReferenceCalls.Should().Be(0);
        injector.GetState(runId).ExpectedPopulation.Should().Equal("remain");
        injector.GetState(runId).DischargedDuringWindow.Should().Equal("remain");
    }

    [Fact]
    public void Pool_builder_marks_imports_and_keeps_cohort_patterns()
    {
        var profiles = new List<PatientProfile>
        {
            new(
                new Dictionary<ProfiledMeasureType, MeasureEligibility>(),
                0,
                "s1",
                10,
                ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod),
            new(
                new Dictionary<ProfiledMeasureType, MeasureEligibility>(),
                1,
                "s1",
                10,
                ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod)
        };

        var seeds = LivePatientPoolBuilder.Build(
            ["cohort-1", "import-1"],
            profiles,
            ["import-1", "import-2"]);

        seeds.Should().Contain(s => s.PatientId == "cohort-1" && s.Origin == LivePatientOrigin.Cohort && s.Pattern == ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod);
        seeds.Should().Contain(s => s.PatientId == "import-1" && s.Origin == LivePatientOrigin.Import && s.Pattern == null);
        seeds.Should().Contain(s => s.PatientId == "import-2" && s.Origin == LivePatientOrigin.Import);
    }

    [Fact]
    public void Pool_builder_uses_manifest_expected_ids_not_census()
    {
        var profiles = new List<PatientProfile>
        {
            new(
                new Dictionary<ProfiledMeasureType, MeasureEligibility>(),
                0,
                "s1",
                10,
                ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod)
        };

        var seeds = LivePatientPoolBuilder.Build(
            ["remain", "outside"],
            profiles,
            importedPatientIds: null,
            expectedInReportPatientIds: new HashSet<string>(StringComparer.Ordinal) { "remain" });

        seeds.Should().Contain(s => s.PatientId == "remain" && s.ExpectedInReport == true);
        seeds.Should().Contain(s => s.PatientId == "outside" && s.ExpectedInReport == false);
    }

    private static LivePatientSeed Seed(string id, ScheduledInpatientPattern pattern)
        => new()
        {
            PatientId = id,
            Origin = LivePatientOrigin.Cohort,
            Pattern = pattern
        };

    private static LivePatientEventInjector CreateInjector(ILivePatientProvisioner? provisioner = null)
    {
        _ = provisioner;
        var store = new Mock<ISnapshotStore>();
        store.Setup(s => s.SetDomainAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<LiveSimulationDiagnostics>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        var proxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<RunHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        return new LivePatientEventInjector(store.Object, hub.Object, NullLogger<LivePatientEventInjector>.Instance);
    }

    private sealed class RecordingProvisioner : ILivePatientProvisioner
    {
        public int GenerateCalls { get; private set; }
        public int UploadCalls { get; private set; }
        public int ReferenceCalls { get; private set; }

        public Task<LiveProvisionedPatient> GenerateQualifyingPatientAsync(CancellationToken cancellationToken)
        {
            GenerateCalls++;
            return Task.FromResult(new LiveProvisionedPatient("gen-appended", ExpectedInReport: true));
        }

        public Task<LiveProvisionedPatient> UploadPatientAsync(string content, string? fileName, CancellationToken cancellationToken)
        {
            UploadCalls++;
            return Task.FromResult(new LiveProvisionedPatient("upload-appended", ExpectedInReport: true));
        }

        public Task<LiveProvisionedPatient> ReferencePatientAsync(string patientId, CancellationToken cancellationToken)
        {
            ReferenceCalls++;
            return Task.FromResult(new LiveProvisionedPatient("ref-appended", ExpectedInReport: false));
        }
    }
}
