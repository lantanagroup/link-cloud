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

    private sealed class Harness
    {
        public ReportScheduleModel Schedule { get; }
        public ReportManifestProducer Producer { get; }

        public Harness()
        {
            Schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid(),
                FacilityId = FacilityId,
                ReportStartDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ReportEndDate = new DateTimeOffset(2025, 1, 31, 23, 59, 59, TimeSpan.Zero),
                ReportTypes = [Measure]
            };

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

            var submitPayloadProducer = new SubmitPayloadProducer(
                scopeFactory,
                Mock.Of<IProducer<SubmitPayloadKey, SubmitPayloadValue>>(),
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
                new BlobStorageService(blobSettings),
                submitPayloadProducer,
                auditableEventOccurredProducer,
                Mock.Of<IReportEntryManager>());
        }
    }
}
