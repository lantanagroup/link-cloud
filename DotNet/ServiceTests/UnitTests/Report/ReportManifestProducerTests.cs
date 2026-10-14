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

        var resources = await harness.Producer.Generate(harness.Schedule);

        var device = Assert.Single(resources.OfType<Device>());
        Assert.Equal(
            new[] { "http://hl7.org/fhir/us/nhsn-dqm/StructureDefinition/nhsn-submitting-device" },
            device.Meta?.Profile);
    }

    [Fact]
    public async Task Generate_StampsPoiListProfileOnPatientCensusList()
    {
        var harness = new Harness();

        var resources = await harness.Producer.Generate(harness.Schedule);

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

        var resources = await harness.Producer.Generate(harness.Schedule);

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

        var resources = await harness.Producer.Generate(harness.Schedule);

        Assert.Equal(
            new[] { ReportConstants.BundleSettings.SubmittingDeviceProfile },
            resources.OfType<Device>().Single().Meta?.Profile);
        Assert.Equal(
            new[] { ReportConstants.BundleSettings.CensusProfileUrl },
            resources.OfType<List>().Single().Meta?.Profile);
    }

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

    private sealed class Harness
    {
        public ReportScheduleModel Schedule { get; }
        public ReportManifestProducer Producer { get; }
        public Mock<IReportScheduledManager> ScheduleManager { get; } = new();
        public Mock<IReportEntryManager> EntryManager { get; } = new();
        public Mock<BlobStorageService> BlobStorage { get; }
        public Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> SubmitPayloadKafkaProducer { get; } = new();

        /// <param name="enableSubmission">
        /// False models a report requested with bypassSubmission: true.
        /// </param>
        /// <param name="endOfReportPeriodJobHasRun">
        /// Produce short-circuits before doing anything unless this is true.
        /// </param>
        /// <param name="allEntriesComplete">
        /// The second gate Produce checks. False models patients still in flight.
        /// </param>
        public Harness(
            bool enableSubmission = true,
            bool endOfReportPeriodJobHasRun = true,
            bool allEntriesComplete = true)
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
                [
                    new ReportEntry
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = FacilityId,
                        ReportScheduleId = Schedule.Id,
                        PatientId = PatientId,
                        ReportingStatus = ReportingStatus.PassedValidation,
                        SubmissionStatus = SubmissionStatus.Submitted
                    }
                ]);

            var database = new Mock<IDatabase>();
            database.SetupGet(d => d.ReportEntryRepository).Returns(reportEntryRepository.Object);

            var services = new ServiceCollection();
            services.AddScoped(_ => database.Object);
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var tenantApiService = new Mock<ITenantApiService>();
            tenantApiService
                .Setup(t => t.GetFacilityConfig(FacilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FacilityModel { FacilityId = FacilityId, FacilityName = FacilityName });

            var reportPopulationManager = new Mock<IReportPopulationManager>();
            reportPopulationManager
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
                reportPopulationManager.Object);

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
                Mock.Of<IProducer<string, AuditEventMessage>>(),
                new ServiceInformation { ServiceConfigName = "Report" });

            Producer = new ReportManifestProducer(
                Mock.Of<ILogger<ReportManifestProducer>>(),
                scopeFactory,
                aggregator,
                tenantApiService.Object,
                BlobStorage.Object,
                submitPayloadProducer,
                auditableEventOccurredProducer,
                EntryManager.Object,
                ScheduleManager.Object);
        }
    }
}
