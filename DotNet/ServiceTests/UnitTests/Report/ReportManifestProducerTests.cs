using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Linq.Expressions;
using List = Hl7.Fhir.Model.List;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers the meta.profile stamped on the resources that make up manifest.ndjson (LEGLINK-871).
/// Downstream NHSN DQM IG validation cannot resolve a resource to its profile without it.
/// </summary>
[Trait("Category", "UnitTests")]
public class ReportManifestProducerTests
{
    private const string FacilityId = "facility-a";
    private const string FacilityName = "Facility A";
    private const string PatientId = "patient-1";
    private const string Measure = "NHSNdQMAcuteCareHospitalInitialPopulation";

    [Fact]
    public async Task Generate_StampsSubmittingDeviceProfileOnDevice()
    {
        var harness = new Harness();

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var device = Assert.Single(resources.OfType<Device>());
        Assert.Equal(
            new[] { "http://hl7.org/fhir/us/nhsn-dqm/StructureDefinition/nhsn-submitting-device" },
            device.Meta?.Profile);
    }

    [Fact]
    public async Task Generate_StampsPoiListProfileOnPatientCensusList()
    {
        var harness = new Harness();

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var patientList = Assert.Single(resources.OfType<List>());
        Assert.Equal(
            new[] { "http://hl7.org/fhir/us/nhsn-dqm/StructureDefinition/poi-list" },
            patientList.Meta?.Profile);
    }

    /// <summary>
    /// The profiles that already existed must survive, and the manifest shape must not change.
    /// </summary>
    [Fact]
    public async Task Generate_LeavesOrganizationAndAggregateMeasureReportProfilesUnchanged()
    {
        var harness = new Harness();

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var organization = Assert.Single(resources.OfType<Organization>());
        Assert.Equal(
            new[] { "https://www.cdc.gov/nhsn/nhsn-measures/StructureDefinition/nhsn-submitting-organization" },
            organization.Meta?.Profile);

        var measureReport = Assert.Single(resources.OfType<MeasureReport>());
        Assert.Equal(
            new[] { "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/subjectlist-measurereport" },
            measureReport.Meta?.Profile);

        Assert.Single(resources.OfType<Device>());
        Assert.Single(resources.OfType<List>());
        Assert.Equal(4, resources.Count);
    }

    [Fact]
    public async Task Generate_ProfileUrlsComeFromBundleSettingsConstants()
    {
        var harness = new Harness();

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        Assert.Equal(
            new[] { ReportConstants.BundleSettings.SubmittingDeviceProfile },
            resources.OfType<Device>().Single().Meta?.Profile);
        Assert.Equal(
            new[] { ReportConstants.BundleSettings.CensusProfileUrl },
            resources.OfType<List>().Single().Meta?.Profile);
    }

    #region Submitting Device detail (LEGLINK-1420)

    /// <summary>
    /// deviceName.type is 1..1 in base R4, so without it the Device fails before the profile is applied.
    /// </summary>
    [Fact]
    public async Task Generate_Device_DeviceNameTypeIsUserFriendlyName()
    {
        var harness = new Harness();

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var deviceName = Assert.Single(Assert.Single(resources.OfType<Device>()).DeviceName);
        Assert.Equal(ReportConstants.BundleSettings.DeviceName, deviceName.Name);
        Assert.Equal(DeviceNameType.UserFriendlyName, deviceName.Type);
    }

    /// <summary>
    /// The same three typed entries MVP Link submitted, in the same order, each with component "api".
    /// </summary>
    [Fact]
    public async Task Generate_Device_EmitsTypedVersionBuildAndCommitEntries()
    {
        var harness = new Harness(serviceInformation: new ServiceInformation
        {
            ServiceConfigName = "Report",
            Version = "1.2.3",
            Build = "20261026.1",
            Commit = "abc1234"
        });

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var versions = Assert.Single(resources.OfType<Device>()).Version;
        Assert.Collection(versions,
            v => AssertDeviceVersion(v, "version", null, "1.2.3"),
            v => AssertDeviceVersion(v, "build", ReportConstants.BundleSettings.LinkDeviceVersionCodeSystem, "20261026.1"),
            v => AssertDeviceVersion(v, "commit", ReportConstants.BundleSettings.LinkDeviceVersionCodeSystem, "abc1234"));
    }

    [Fact]
    public async Task Generate_Device_ProductVersionSet_IsPreferredOverVersion()
    {
        var harness = new Harness(serviceInformation: new ServiceInformation
        {
            ServiceConfigName = "Report",
            Version = "1.2.3",
            ProductVersion = "0.9.0"
        });

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var version = Assert.Single(Assert.Single(resources.OfType<Device>()).Version);
        AssertDeviceVersion(version, "version", null, "0.9.0");
    }

    /// <summary>
    /// Build and commit are injected by CI, so a local stack has neither. An empty string is not valid
    /// FHIR, so the entry is left out rather than written empty, which is what the bug shipped.
    /// </summary>
    [Fact]
    public async Task Generate_Device_BuildAndCommitEmpty_OmitsThoseEntries()
    {
        var harness = new Harness(serviceInformation: new ServiceInformation
        {
            ServiceConfigName = "Report",
            Version = "1.2.3"
        });

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var device = Assert.Single(resources.OfType<Device>());
        var version = Assert.Single(device.Version);
        AssertDeviceVersion(version, "version", null, "1.2.3");
        Assert.DoesNotContain(device.Version, v => string.IsNullOrEmpty(v.Value));
    }

    private static void AssertDeviceVersion(Device.VersionComponent actual, string code, string? system, string value)
    {
        var coding = Assert.Single(actual.Type.Coding);
        Assert.Equal(code, coding.Code);
        Assert.Equal(system, coding.System);
        Assert.Equal(ReportConstants.BundleSettings.DeviceVersionComponent, actual.Component.Value);
        Assert.Equal(value, actual.Value);
    }

    #endregion

    #region Submission gating (LEGLINK-1083)

    /// <summary>
    /// The report-level half of the bypassSubmission defect. This produce is what copies
    /// manifest.ndjson from internal/ to external/, and that copy IS the submission --
    /// nothing downstream of external/ lives in this codebase, so it cannot be recalled.
    /// </summary>
    [Fact]
    public async Task Produce_SubmissionBypassed_ProducesNoSubmitPayload()
    {
        var harness = new Harness(enableSubmission: false);

        var produced = await harness.Producer.Produce(harness.Schedule);

        Assert.True(produced);
        harness.SubmitPayloadKafkaProducer.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<SubmitPayloadKey, SubmitPayloadValue>>(),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
            Times.Never);
    }

    /// <summary>
    /// Bypassing submission means not publishing to external/, not skipping the work. The
    /// manifest is still built and still written to internal/.
    /// </summary>
    [Fact]
    public async Task Produce_SubmissionBypassed_StillUploadsManifestToInternal()
    {
        var harness = new Harness(enableSubmission: false);

        await harness.Producer.Produce(harness.Schedule);

        harness.BlobStorage.Verify(
            b => b.UploadManifestAsync(
                harness.Schedule,
                It.Is<IEnumerable<Resource>>(r => r.Any()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// ScheduleStatus.Submitted is only ever assigned by PayloadSubmittedListener, reacting
    /// to the manifest's own PayloadSubmitted event. With no submission there is no event,
    /// so without assigning a terminal status here the schedule would sit at EndOfPeriod
    /// forever and read as Pending to the facility.
    /// </summary>
    [Fact]
    public async Task Produce_SubmissionBypassed_SetsCompletedNotSubmitted()
    {
        var harness = new Harness(enableSubmission: false);

        await harness.Producer.Produce(harness.Schedule);

        Assert.Equal(ScheduleStatus.CompletedNotSubmitted, harness.Schedule.Status);
        Assert.True(harness.Schedule.Status.IsTerminal());

        harness.ScheduleManager.Verify(
            m => m.UpdateAsync(
                It.Is<ReportScheduleModel>(s =>
                    s.Id == harness.Schedule.Id &&
                    s.Status == ScheduleStatus.CompletedNotSubmitted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The timestamp records an actual submission. Leaving it null is what keeps
    /// CompletedNotSubmitted from being distinguishable only by a missing value.
    /// </summary>
    [Fact]
    public async Task Produce_SubmissionBypassed_LeavesSubmitReportDateTimeNull()
    {
        var harness = new Harness(enableSubmission: false);

        await harness.Producer.Produce(harness.Schedule);

        Assert.Null(harness.Schedule.SubmitReportDateTime);
    }

    /// <summary>
    /// EnableSubmission defaults to true, so an ordinary report must be untouched by the
    /// gate: it still produces, and its status still advances via PayloadSubmittedListener
    /// rather than being written here.
    /// </summary>
    [Fact]
    public async Task Produce_SubmissionEnabled_ProducesSubmitPayloadAndDoesNotWriteStatus()
    {
        var harness = new Harness();
        Assert.True(harness.Schedule.EnableSubmission);

        var produced = await harness.Producer.Produce(harness.Schedule);

        Assert.True(produced);
        harness.SubmitPayloadKafkaProducer.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Value.PayloadType == PayloadType.ReportSchedule),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
            Times.Once);

        Assert.Equal(ScheduleStatus.EndOfPeriod, harness.Schedule.Status);
        harness.ScheduleManager.Verify(
            m => m.UpdateAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Both existing gates must still short-circuit ahead of the bypass branch. A report
    /// that reached neither must not be marked terminal, and must not have its manifest
    /// uploaded, merely because submission is disabled.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Produce_SubmissionBypassedButNotReady_DoesNothing(
        bool endOfReportPeriodJobHasRun,
        bool allEntriesComplete)
    {
        var harness = new Harness(
            enableSubmission: false,
            endOfReportPeriodJobHasRun: endOfReportPeriodJobHasRun,
            allEntriesComplete: allEntriesComplete);

        var produced = await harness.Producer.Produce(harness.Schedule);

        Assert.False(produced);
        Assert.Equal(ScheduleStatus.EndOfPeriod, harness.Schedule.Status);

        harness.BlobStorage.Verify(
            b => b.UploadManifestAsync(
                It.IsAny<ReportScheduleModel>(),
                It.IsAny<IEnumerable<Resource>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ScheduleManager.Verify(
            m => m.UpdateAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// A failed manifest upload returns false before either branch runs. A bypassed report
    /// must not be marked complete when its manifest never reached internal/.
    /// </summary>
    [Fact]
    public async Task Produce_SubmissionBypassedAndUploadFails_DoesNotSetTerminalStatus()
    {
        var harness = new Harness(enableSubmission: false);
        harness.BlobStorage
            .Setup(b => b.UploadManifestAsync(
                It.IsAny<ReportScheduleModel>(),
                It.IsAny<IEnumerable<Resource>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob storage unavailable"));

        var produced = await harness.Producer.Produce(harness.Schedule);

        Assert.False(produced);
        Assert.Equal(ScheduleStatus.EndOfPeriod, harness.Schedule.Status);
        harness.ScheduleManager.Verify(
            m => m.UpdateAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Ad-hoc generation with patients in flight (LEGLINK-1864)

    /// <summary>
    /// Every way an entry can stall short of the terminal sets the completion gate uses. A patient
    /// stalled before aggregation would otherwise look exactly like a NotReportable one, and one
    /// stalled after it is already counted in the aggregates.
    /// </summary>
    [Theory]
    [InlineData(ReportingStatus.PatientIdentified, null)]
    [InlineData(ReportingStatus.PendingValidation, SubmissionStatus.PendingValidation)]
    [InlineData(ReportingStatus.PassedValidation, SubmissionStatus.Submitting)]
    [InlineData(ReportingStatus.PassedValidation, SubmissionStatus.FailedSubmission)]
    [InlineData(ReportingStatus.PassedValidation, null)]
    [InlineData(ReportingStatus.NotReportable, null)]
    public async Task Generate_NonTerminalEntry_AddsOneIncompleteWarning(ReportingStatus reporting, SubmissionStatus? submission)
    {
        var harness = new Harness(entries:
        [
            (PatientId, ReportingStatus.PassedValidation, SubmissionStatus.Submitted),
            ("patient-stalled", reporting, submission)
        ]);

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var issue = Assert.Single(Assert.Single(resources.OfType<OperationOutcome>()).Issue);
        Assert.Equal(OperationOutcome.IssueSeverity.Warning, issue.Severity);
        Assert.Equal(OperationOutcome.IssueType.Incomplete, issue.Code);
        Assert.Contains("patient-stalled", issue.Diagnostics);
        Assert.Contains(reporting.ToString(), issue.Diagnostics);
        Assert.Contains(submission?.ToString() ?? "None", issue.Diagnostics);
    }

    /// <summary>
    /// What the scheduled path sees: Produce only runs once every entry is terminal, so the
    /// stalled-patient warnings must never reach an uploaded manifest.
    /// </summary>
    [Fact]
    public async Task Generate_AllEntriesTerminal_AddsNoIncompleteWarnings()
    {
        var harness = new Harness(entries:
        [
            ("p-submitted", ReportingStatus.PassedValidation, SubmissionStatus.Submitted),
            ("p-not-reportable", ReportingStatus.NotReportable, SubmissionStatus.NotEligable),
            ("p-bypassed", ReportingStatus.PassedValidation, SubmissionStatus.NotSubmitted),
            ("p-failed", ReportingStatus.FailedValidation, SubmissionStatus.Submitted)
        ]);

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var issue = Assert.Single(Assert.Single(resources.OfType<OperationOutcome>()).Issue);
        Assert.Equal(OperationOutcome.IssueSeverity.Error, issue.Severity);
        Assert.Equal(OperationOutcome.IssueType.Invalid, issue.Code);
        Assert.Contains("p-failed", issue.Diagnostics);
    }

    /// <summary>
    /// A failed patient whose submission is also stuck gets both issues.
    /// </summary>
    [Fact]
    public async Task Generate_FailedValidationAndStalledSubmission_GetsErrorAndWarning()
    {
        var harness = new Harness(entries: [(PatientId, ReportingStatus.FailedValidation, SubmissionStatus.Submitting)]);

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var issues = Assert.Single(resources.OfType<OperationOutcome>()).Issue;
        Assert.Collection(issues,
            i => Assert.Equal(OperationOutcome.IssueType.Invalid, i.Code),
            i => Assert.Equal(OperationOutcome.IssueType.Incomplete, i.Code));
    }

    [Fact]
    public async Task Generate_DuplicateEntriesForOnePatient_ListsPatientOnce()
    {
        var harness = new Harness(entries:
        [
            (PatientId, ReportingStatus.PassedValidation, SubmissionStatus.Submitted),
            (PatientId, ReportingStatus.PassedValidation, SubmissionStatus.Submitted),
            ("patient-2", ReportingStatus.NotReportable, SubmissionStatus.NotEligable)
        ]);

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var census = Assert.Single(resources.OfType<List>());
        Assert.Equal(
            new[] { $"Patient/{PatientId}", "Patient/patient-2" },
            census.Entry.Select(e => e.Item.Reference));
    }

    /// <summary>
    /// Only a forced ad-hoc request reaches Generate before the period closes; Produce returns
    /// first. The manifest has to say the census may still change.
    /// </summary>
    [Fact]
    public async Task Generate_PeriodNotClosed_AddsPeriodWarning()
    {
        var harness = new Harness(endOfReportPeriodJobHasRun: false);

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);

        var issue = Assert.Single(Assert.Single(resources.OfType<OperationOutcome>()).Issue);
        Assert.Equal(OperationOutcome.IssueSeverity.Warning, issue.Severity);
        Assert.Equal(OperationOutcome.IssueType.Informational, issue.Code);
        Assert.Contains("reporting period has not closed", issue.Diagnostics);
    }

    [Fact]
    public async Task Generate_TenantReportsFacilityMissing_ThrowsFacilityConfigNotFound()
    {
        var harness = new Harness();
        harness.TenantApiService
            .Setup(t => t.GetFacilityConfig(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No Facility Config found"));

        await Assert.ThrowsAsync<LantanaGroup.Link.Report.Application.FacilityConfigNotFoundException>(
            () => harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc));
    }

    [Fact]
    public async Task Generate_TenantReturnsNoConfig_ThrowsFacilityConfigNotFound()
    {
        var harness = new Harness();
        harness.TenantApiService
            .Setup(t => t.GetFacilityConfig(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FacilityModel)null!);

        await Assert.ThrowsAsync<LantanaGroup.Link.Report.Application.FacilityConfigNotFoundException>(
            () => harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc));
    }

    [Fact]
    public async Task Generate_TenantFails_ThrowsTenantUnavailable()
    {
        var harness = new Harness();
        harness.TenantApiService
            .Setup(t => t.GetFacilityConfig(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<LantanaGroup.Link.Report.Application.TenantUnavailableException>(
            () => harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc));
    }

    /// <summary>
    /// The ad-hoc endpoint relies on Generate and GenerateAsBundle writing nothing anywhere.
    /// </summary>
    [Fact]
    public async Task GenerateAndGenerateAsBundle_WithPatientsInFlight_HaveNoSideEffects()
    {
        var harness = new Harness(
            endOfReportPeriodJobHasRun: false,
            allEntriesComplete: false,
            entries: [(PatientId, ReportingStatus.PendingValidation, SubmissionStatus.PendingValidation)]);

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc);
        var bundle = await harness.Producer.GenerateAsBundle(harness.Schedule, ManifestGenerationMode.AdHoc);

        Assert.Equal(Bundle.BundleType.Collection, bundle.Type);
        Assert.Equal(
            resources.Select(r => r.TypeName),
            bundle.Entry.Select(e => e.Resource.TypeName));

        harness.BlobStorage.Verify(
            b => b.UploadManifestAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.SubmitPayloadKafkaProducer.VerifyNoOtherCalls();
        harness.AuditKafkaProducer.VerifyNoOtherCalls();
        harness.ScheduleManager.Verify(
            m => m.UpdateAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.EntryManager.Verify(
            m => m.AreAllEntriesCompleteAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(ScheduleStatus.EndOfPeriod, harness.Schedule.Status);
    }

    /// <summary>
    /// Entries are read again after the completion gate passes, so one can change in between. The
    /// scheduled manifest is what gets submitted, so it must not carry in-flight or open-period warnings
    /// whatever state the entries are in.
    /// </summary>
    [Fact]
    public async Task Generate_ScheduledMode_AddsNoInFlightOrPeriodWarnings()
    {
        var harness = new Harness(
            endOfReportPeriodJobHasRun: false,
            entries:
            [
                (PatientId, ReportingStatus.PendingValidation, SubmissionStatus.PendingValidation),
                ("p-failed", ReportingStatus.FailedValidation, SubmissionStatus.Submitting)
            ]);

        var resources = await harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.Scheduled);

        var issue = Assert.Single(Assert.Single(resources.OfType<OperationOutcome>()).Issue);
        Assert.Equal(OperationOutcome.IssueType.Invalid, issue.Code);
        Assert.Contains("p-failed", issue.Diagnostics);
    }

    /// <summary>
    /// The same race, observed through Produce: the gate says complete, but by the time Generate reads
    /// the entries one is in flight. The uploaded manifest still carries no incomplete warning.
    /// </summary>
    [Fact]
    public async Task Produce_EntryChangesAfterGate_UploadsManifestWithoutIncompleteWarning()
    {
        var harness = new Harness(entries:
        [
            (PatientId, ReportingStatus.PassedValidation, SubmissionStatus.Submitted),
            ("p-late", ReportingStatus.PatientIdentified, null)
        ]);
        List<Resource>? uploaded = null;
        harness.BlobStorage
            .Setup(b => b.UploadManifestAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()))
            .Callback<ReportScheduleModel, IEnumerable<Resource>, CancellationToken>((_, r, _) => uploaded = r.ToList())
            .ReturnsAsync(new Uri("https://blob.example.com/internal/manifest.ndjson"));

        Assert.True(await harness.Producer.Produce(harness.Schedule));

        Assert.NotNull(uploaded);
        Assert.Empty(uploaded!.OfType<OperationOutcome>());
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Cancelled while Tenant is answering: the census loop is the next per-patient loop, and it must
    /// stop there, before the aggregates are read at all.
    /// </summary>
    [Fact]
    public async Task Generate_CancelledBeforeCensus_StopsInPatientListLoop()
    {
        using var cts = new CancellationTokenSource();
        var harness = new Harness();
        harness.TenantApiService
            .Setup(t => t.GetFacilityConfig(FacilityId, It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new FacilityModel { FacilityId = FacilityId, FacilityName = FacilityName });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Producer.Generate(harness.Schedule, ManifestGenerationMode.AdHoc, cts.Token));

        harness.PopulationManager.Verify(
            m => m.FindAsync(It.IsAny<Expression<Func<ReportPopulation, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Cancelled once the aggregates are built: the OperationOutcome's loop over the entries checks the
    /// token per patient instead of finishing the diagnostics.
    /// </summary>
    [Theory]
    [InlineData(ManifestGenerationMode.AdHoc)]
    [InlineData(ManifestGenerationMode.Scheduled)]
    public async Task Generate_CancelledAfterAggregation_StopsInOperationOutcomeLoop(ManifestGenerationMode mode)
    {
        using var cts = new CancellationTokenSource();
        var harness = new Harness(entries: [(PatientId, ReportingStatus.PendingValidation, SubmissionStatus.PendingValidation)]);
        harness.PopulationManager
            .Setup(m => m.FindAsync(It.IsAny<Expression<Func<ReportPopulation, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync([]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Producer.Generate(harness.Schedule, mode, cts.Token));
    }

    /// <summary>
    /// A cancelled upload is a shutdown or an abandoned request, not a blob storage failure, so it must
    /// propagate rather than be reported as one through a failure audit event.
    /// </summary>
    [Fact]
    public async Task Produce_UploadCancelled_PropagatesWithoutFailureAuditEvent()
    {
        using var cts = new CancellationTokenSource();
        var harness = new Harness(enableSubmission: false);
        harness.BlobStorage
            .Setup(b => b.UploadManifestAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()))
            .Returns<ReportScheduleModel, IEnumerable<Resource>, CancellationToken>((_, _, token) =>
            {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult<Uri?>(null);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Producer.Produce(harness.Schedule, cancellationToken: cts.Token));

        harness.AuditKafkaProducer.VerifyNoOtherCalls();
        harness.ScheduleManager.Verify(
            m => m.UpdateAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(ScheduleStatus.EndOfPeriod, harness.Schedule.Status);
    }

    /// <summary>
    /// The guard is narrow: an OperationCanceledException that is not the caller's cancellation (an
    /// HTTP timeout inside the blob SDK, say) is still an upload failure, audited as before.
    /// </summary>
    [Fact]
    public async Task Produce_UploadTimesOutWithoutCallerCancellation_StillAuditsFailure()
    {
        var harness = new Harness(enableSubmission: false);
        harness.BlobStorage
            .Setup(b => b.UploadManifestAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("upload timed out"));

        var produced = await harness.Producer.Produce(harness.Schedule);

        Assert.False(produced);
        harness.AuditKafkaProducer.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, AuditEventMessage>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    private sealed class Harness
    {
        public ReportScheduleModel Schedule { get; }
        public ReportManifestProducer Producer { get; }
        public Mock<IReportScheduledManager> ScheduleManager { get; } = new();
        public Mock<IReportEntryManager> EntryManager { get; } = new();
        public Mock<BlobStorageService> BlobStorage { get; }
        public Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> SubmitPayloadKafkaProducer { get; } = new();
        public Mock<IProducer<string, AuditEventMessage>> AuditKafkaProducer { get; } = new();
        public Mock<ITenantApiService> TenantApiService { get; } = new();
        public Mock<IReportPopulationManager> PopulationManager { get; } = new();

        /// <param name="enableSubmission">
        /// False models a report requested with bypassSubmission: true.
        /// </param>
        /// <param name="endOfReportPeriodJobHasRun">
        /// Produce short-circuits before doing anything unless this is true.
        /// </param>
        /// <param name="allEntriesComplete">
        /// The second gate Produce checks. False models patients still in flight.
        /// </param>
        /// <param name="serviceInformation">
        /// What SetupServiceInformation registers at startup. Defaults to a CI-shaped one with all three
        /// version values present.
        /// </param>
        /// <param name="entries">
        /// The schedule's report entries, as (patient id, reporting status, submission status). Defaults
        /// to one submitted patient.
        /// </param>
        public Harness(
            bool enableSubmission = true,
            bool endOfReportPeriodJobHasRun = true,
            bool allEntriesComplete = true,
            ServiceInformation? serviceInformation = null,
            IEnumerable<(string PatientId, ReportingStatus Reporting, SubmissionStatus? Submission)>? entries = null)
        {
            Schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid(),
                FacilityId = FacilityId,
                ReportStartDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ReportEndDate = new DateTimeOffset(2025, 1, 31, 23, 59, 59, TimeSpan.Zero),
                ReportTypes = [Measure],
                EnableSubmission = enableSubmission,
                EndOfReportPeriodJobHasRun = endOfReportPeriodJobHasRun,
                Status = ScheduleStatus.EndOfPeriod
            };

            EntryManager
                .Setup(m => m.AreAllEntriesCompleteAsync(FacilityId, Schedule.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(allEntriesComplete);

            ScheduleManager
                .Setup(m => m.UpdateAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReportScheduleModel m, CancellationToken _) => m);

            var reportEntryRepository = new Mock<IEntityRepository<ReportEntry>>();
            reportEntryRepository
                .Setup(r => r.FindAsync(It.IsAny<Expression<Func<ReportEntry, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    (entries ?? [(PatientId, ReportingStatus.PassedValidation, SubmissionStatus.Submitted)])
                    .Select(e => new ReportEntry
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = FacilityId,
                        ReportScheduleId = Schedule.Id,
                        PatientId = e.PatientId,
                        ReportingStatus = e.Reporting,
                        SubmissionStatus = e.Submission
                    })
                    .ToList());

            var database = new Mock<IDatabase>();
            database.SetupGet(d => d.ReportEntryRepository).Returns(reportEntryRepository.Object);

            var services = new ServiceCollection();
            services.AddScoped(_ => database.Object);
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            TenantApiService
                .Setup(t => t.GetFacilityConfig(FacilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FacilityModel { FacilityId = FacilityId, FacilityName = FacilityName });

            PopulationManager
                .Setup(m => m.FindAsync(It.IsAny<Expression<Func<ReportPopulation, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new ReportPopulationModel
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = FacilityId,
                        ReportScheduleId = Schedule.Id,
                        Measure = Measure,
                        ReportType = Measure,
                        GroupPopulations =
                        [
                            new GroupPopulationModel
                            {
                                PopulationId = "initial-population",
                                PopulationCodeJson = """{"coding":[{"code":"initial-population"}]}""",
                                TotalPopulationCount = 1,
                                MeasureReportPopulations =
                                [
                                    new MeasureReportPopulationModel { MeasureReportId = "mr-1", PopulationCount = 1 }
                                ]
                            }
                        ]
                    }
                ]);

            var aggregator = new MeasureReportAggregator(
                Mock.Of<ILogger<MeasureReportAggregator>>(),
                PopulationManager.Object);

            var blobSettings = Options.Create(new BlobStorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "internal"
            });

            // Virtual, so the manifest upload can be observed without reaching Azurite.
            BlobStorage = new Mock<BlobStorageService>(blobSettings) { CallBase = false };
            BlobStorage
                .Setup(b => b.UploadManifestAsync(
                    It.IsAny<ReportScheduleModel>(),
                    It.IsAny<IEnumerable<Resource>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Uri("https://blob.example.com/internal/manifest.ndjson"));

            var submitPayloadProducer = new SubmitPayloadProducer(
                scopeFactory,
                SubmitPayloadKafkaProducer.Object,
                Mock.Of<ILogger<SubmitPayloadProducer>>());

            var auditableEventOccurredProducer = new AuditableEventOccurredProducer(
                Mock.Of<ILogger<AuditableEventOccurredProducer>>(),
                AuditKafkaProducer.Object,
                new ServiceInformation { ServiceConfigName = "Report" });

            Producer = new ReportManifestProducer(
                Mock.Of<ILogger<ReportManifestProducer>>(),
                scopeFactory,
                aggregator,
                TenantApiService.Object,
                BlobStorage.Object,
                submitPayloadProducer,
                auditableEventOccurredProducer,
                EntryManager.Object,
                ScheduleManager.Object,
                serviceInformation ?? new ServiceInformation
                {
                    ServiceConfigName = "Report",
                    Version = "1.2.3",
                    Build = "20261026.1",
                    Commit = "abc1234"
                });
        }
    }
}
