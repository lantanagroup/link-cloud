using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Core
{
    [Collection("IntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class PatientAggregatorTests
    {
        private readonly ReportIntegrationTestFixture _fixture;

        public PatientAggregatorTests(ReportIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private static ReportScheduleModel BuildSchedule(string facilityId)
        {
            return new ReportScheduleModel
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                ReportStartDate = DateTimeOffset.UtcNow.AddDays(-30),
                ReportEndDate = DateTimeOffset.UtcNow,
                Frequency = Frequency.Monthly,
                ReportTypes = { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                CreateDate = DateTime.UtcNow
            };
        }

        private static async Task SeedScheduleAsync(ReportDbContext context, ReportScheduleModel schedule)
        {
            var entity = new ReportSchedule
            {
                Id = schedule.Id,
                CreateDate = DateTime.UtcNow,
                FacilityId = schedule.FacilityId,
                ReportStartDate = schedule.ReportStartDate,
                ReportEndDate = schedule.ReportEndDate,
                Frequency = schedule.Frequency,
                Status = schedule.Status
            };

            foreach (var rt in schedule.ReportTypes)
            {
                entity.ReportTypes.Add(new ScheduleReportType { ReportType = rt });
            }

            context.ReportSchedule.Add(entity);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private static async Task SeedReportEntryAsync(
            ReportDbContext context,
            Guid reportScheduleId,
            string facilityId,
            string patientId,
            params (string reportType, MeasureReportStatus status, string? measureReportId, string? blobFileName)[] measureReports)
        {
            var entry = new ReportEntry
            {
                Id = Guid.NewGuid(),
                CreateDate = DateTime.UtcNow,
                FacilityId = facilityId,
                ReportScheduleId = reportScheduleId,
                PatientId = patientId,
                ReportingStatus = ReportingStatus.PatientIdentified
            };

            foreach (var (reportType, status, measureReportId, blobFileName) in measureReports)
            {
                entry.MeasureReports.Add(new EntryMeasureReport
                {
                    ReportType = reportType,
                    Status = status,
                    MeasureReportId = measureReportId,
                    MeasureReportFileName = blobFileName
                });
            }

            context.ReportEntry.Add(entry);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        /// <summary>
        /// Uploads an NDJSON blob in the alternating-line format that
        /// <see cref="PatientAggregator.AggregateToABS"/> expects:
        /// line 1 = resource reference (e.g. "MeasureReport/abc-123"),
        /// line 2 = serialized FHIR JSON.
        /// </summary>
        private async Task UploadPatientBlobAsync(
            string blobName,
            params (string resourceReference, string fhirJson)[] lines)
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();

            // Build the NDJSON content
            var sb = new StringBuilder();
            foreach (var (resourceReference, fhirJson) in lines)
            {
                sb.AppendLine(resourceReference);
                sb.AppendLine(fhirJson);
            }

            // Write directly via the Azurite container client
            var connStr = _fixture.AzuriteConnectionString;
            var containerClient = new Azure.Storage.Blobs.BlobContainerClient(connStr, "report-test-container");
            await containerClient.CreateIfNotExistsAsync();

            var appendBlob = containerClient.GetAppendBlobClient(blobName);
            await appendBlob.CreateIfNotExistsAsync();

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            using var stream = new MemoryStream(bytes);
            await appendBlob.AppendBlockAsync(stream);
        }

        // ------------------------------------------------------------------ //
        //  Helpers — IncludeOrganizationResource tests
        // ------------------------------------------------------------------ //

        private PatientAggregator BuildAggregator(IServiceScope scope, bool includeOrganization)
        {
            var metrics = scope.ServiceProvider.GetRequiredService<IReportServiceMetrics>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            var blobSettings = scope.ServiceProvider.GetRequiredService<IOptions<BlobStorageSettings>>();
            var aggregatorSettings = Options.Create(new PatientAggregatorSettings { IncludeOrganizationResource = includeOrganization });
            return new PatientAggregator(metrics, reportEntryManager, blobService, blobSettings, _fixture.TenantApiServiceMock.Object, aggregatorSettings);
        }

        private static Hl7.Fhir.Model.MeasureReport BuildMeasureReport() => new()
        {
            Id = Guid.NewGuid().ToString(),
            Measure = "http://example.com/Measure/DE-111",
            Status = Hl7.Fhir.Model.MeasureReport.MeasureReportStatus.Complete,
            Type = Hl7.Fhir.Model.MeasureReport.MeasureReportType.Individual,
            Group =
            {
                new Hl7.Fhir.Model.MeasureReport.GroupComponent
                {
                    Population =
                    {
                        new Hl7.Fhir.Model.MeasureReport.PopulationComponent
                        {
                            ElementId = "initial-population",
                            Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "initial-population"),
                            Count = 1
                        }
                    }
                }
            }
        };

        private async Task<string[]> ReadOutputBlobLinesAsync(string blobName)
        {
            var containerClient = new BlobContainerClient(_fixture.AzuriteConnectionString, "report-test-container");
            var response = await containerClient.GetBlobClient(blobName).DownloadAsync();
            using var reader = new StreamReader(response.Value.Content);
            var content = await reader.ReadToEndAsync();
            return content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        // ------------------------------------------------------------------ //
        //  IncludeOrganizationResource = false → no Organization in blob
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_IncludeOrganizationResource_False_DoesNotWriteOrganizationToBlob()
        {
            _fixture.TenantApiServiceMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();

            var facilityId = "pat-agg-facility-006";
            var patientId = "patient-006";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            var measureReport = BuildMeasureReport();
            var mrJson = new FhirJsonSerializer().SerializeToString(measureReport);
            var reportName = blobService.GetReportName(schedule);
            var entryBlobName = blobService.GetBlobName(reportName, $"patient-{patientId}-DE-111.mr");
            await UploadPatientBlobAsync(entryBlobName, ($"MeasureReport/{measureReport.Id}", mrJson));
            await SeedReportEntryAsync(context, schedule.Id, facilityId, patientId,
                ("DE-111", MeasureReportStatus.ReadyForValidation, measureReport.Id, entryBlobName));

            var aggregator = BuildAggregator(scope, includeOrganization: false);
            var result = await aggregator.AggregateToABS(patientId, schedule);

            var parser = new FhirJsonParser();
            var lines = await ReadOutputBlobLinesAsync(result.BlobName);
            var hasOrganization = lines.Any(line =>
            {
                try { return parser.Parse<Resource>(line) is Organization; }
                catch { return false; }
            });
            Assert.False(hasOrganization);
        }

        // ------------------------------------------------------------------ //
        //  IncludeOrganizationResource = true → Organization appended
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_IncludeOrganizationResource_True_AppendsOrganizationToBlob()
        {
            _fixture.TenantApiServiceMock.Reset();

            var facilityId = "pat-agg-facility-007";
            var patientId = "patient-007";
            var facilityName = "Test Hospital";

            _fixture.TenantApiServiceMock
                .Setup(x => x.GetFacilityConfig(facilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FacilityModel { FacilityName = facilityName, FacilityId = facilityId });

            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();

            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            var measureReport = BuildMeasureReport();
            var mrJson = new FhirJsonSerializer().SerializeToString(measureReport);
            var reportName = blobService.GetReportName(schedule);
            var entryBlobName = blobService.GetBlobName(reportName, $"patient-{patientId}-DE-111.mr");
            await UploadPatientBlobAsync(entryBlobName, ($"MeasureReport/{measureReport.Id}", mrJson));
            await SeedReportEntryAsync(context, schedule.Id, facilityId, patientId,
                ("DE-111", MeasureReportStatus.ReadyForValidation, measureReport.Id, entryBlobName));

            var aggregator = BuildAggregator(scope, includeOrganization: true);
            var result = await aggregator.AggregateToABS(patientId, schedule);

            var parser = new FhirJsonParser();
            var lines = await ReadOutputBlobLinesAsync(result.BlobName);
            var orgLine = lines.FirstOrDefault(line =>
            {
                try { return parser.Parse<Resource>(line) is Organization; }
                catch { return false; }
            });
            Assert.NotNull(orgLine);
            var org = parser.Parse<Organization>(orgLine);
            Assert.Equal(facilityName, org.Name);
            Assert.Equal(facilityId, org.Identifier.First().Value);
        }

        // ------------------------------------------------------------------ //
        //  IncludeOrganizationResource = true, null facility config → no Organization
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_IncludeOrganizationResource_True_NullFacilityConfig_DoesNotAppendOrganization()
        {
            _fixture.TenantApiServiceMock.Reset();

            var facilityId = "pat-agg-facility-008";
            var patientId = "patient-008";

            _fixture.TenantApiServiceMock
                .Setup(x => x.GetFacilityConfig(facilityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FacilityModel?)null);

            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();

            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            var measureReport = BuildMeasureReport();
            var mrJson = new FhirJsonSerializer().SerializeToString(measureReport);
            var reportName = blobService.GetReportName(schedule);
            var entryBlobName = blobService.GetBlobName(reportName, $"patient-{patientId}-DE-111.mr");
            await UploadPatientBlobAsync(entryBlobName, ($"MeasureReport/{measureReport.Id}", mrJson));
            await SeedReportEntryAsync(context, schedule.Id, facilityId, patientId,
                ("DE-111", MeasureReportStatus.ReadyForValidation, measureReport.Id, entryBlobName));

            var aggregator = BuildAggregator(scope, includeOrganization: true);
            var result = await aggregator.AggregateToABS(patientId, schedule);

            var parser = new FhirJsonParser();
            var lines = await ReadOutputBlobLinesAsync(result.BlobName);
            var hasOrganization = lines.Any(line =>
            {
                try { return parser.Parse<Resource>(line) is Organization; }
                catch { return false; }
            });
            Assert.False(hasOrganization);
        }

        // ------------------------------------------------------------------ //
        //  No entry found ? throws
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_EntryMissing_ThrowsException()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var aggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();

            var facilityId = "pat-agg-facility-001";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            // No ReportEntry seeded for this patient
            var ex = await Assert.ThrowsAsync<Exception>(
                () => aggregator.AggregateToABS("nonexistent-patient", schedule));

            Assert.Contains("No ReportEntry record found", ex.Message);
        }

        // ------------------------------------------------------------------ //
        //  Entry exists but no ReadyForValidation measure reports ? empty results
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_NoReadyForValidationMeasureReports_ReturnsEmptyResults()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var aggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();

            var facilityId = "pat-agg-facility-002";
            var patientId = "patient-002";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportEntryAsync(context, schedule.Id, facilityId, patientId,
                ("DE-111", MeasureReportStatus.NotReportable, null, null));

            var result = await aggregator.AggregateToABS(patientId, schedule);

            Assert.NotNull(result);
            Assert.NotNull(result.BlobName);
            Assert.Contains($"patient-{patientId}", result.BlobName);
            Assert.Empty(result.MeasureReportResults);
        }

        // ------------------------------------------------------------------ //
        //  Single ReadyForValidation measure report ? aggregated correctly
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_SingleReadyMeasureReport_AggregatesCorrectly()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var aggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();

            var facilityId = "pat-agg-facility-003";
            var patientId = "patient-003";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            // Build a minimal FHIR MeasureReport
            var measureReport = new Hl7.Fhir.Model.MeasureReport
            {
                Id = Guid.NewGuid().ToString(),
                Measure = "http://example.com/Measure/DE-111",
                Status = Hl7.Fhir.Model.MeasureReport.MeasureReportStatus.Complete,
                Type = Hl7.Fhir.Model.MeasureReport.MeasureReportType.Individual,
                Group =
                {
                    new Hl7.Fhir.Model.MeasureReport.GroupComponent
                    {
                        Population =
                        {
                            new Hl7.Fhir.Model.MeasureReport.PopulationComponent
                            {
                                ElementId = "initial-population",
                                Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "initial-population"),
                                Count = 1
                            }
                        }
                    }
                }
            };

            var serializer = new FhirJsonSerializer();
            var mrJson = serializer.SerializeToString(measureReport);
            var mrReference = $"MeasureReport/{measureReport.Id}";

            // Determine the blob path that PatientAggregator will read from
            var reportName = blobService.GetReportName(schedule);
            var entryBlobName = blobService.GetBlobName(reportName, $"patient-{patientId}-DE-111.mr");

            await UploadPatientBlobAsync(entryBlobName, (mrReference, mrJson));

            await SeedReportEntryAsync(context, schedule.Id, facilityId, patientId,
                ("DE-111", MeasureReportStatus.ReadyForValidation, measureReport.Id, entryBlobName));

            var result = await aggregator.AggregateToABS(patientId, schedule);

            Assert.NotNull(result);
            Assert.NotNull(result.BlobName);
            Assert.Single(result.MeasureReportResults);

            var aggregatedMr = result.MeasureReportResults[0];
            Assert.Equal("DE-111", aggregatedMr.ReportType);
            Assert.Equal(measureReport.Id, aggregatedMr.MeasureReportId);
            Assert.Equal("http://example.com/Measure/DE-111", aggregatedMr.Measure);
            Assert.Single(aggregatedMr.PopulationList);
            Assert.Equal("initial-population", aggregatedMr.PopulationList[0].PopulationId);
            Assert.Equal(1, aggregatedMr.PopulationList[0].PopulationCount);
        }

        // ------------------------------------------------------------------ //
        //  Duplicate resource references are de-duplicated
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_DuplicateResources_AreSkipped()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var aggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();

            var facilityId = "pat-agg-facility-004";
            var patientId = "patient-004";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            var measureReport = new Hl7.Fhir.Model.MeasureReport
            {
                Id = Guid.NewGuid().ToString(),
                Measure = "http://example.com/Measure/DE-111",
                Status = Hl7.Fhir.Model.MeasureReport.MeasureReportStatus.Complete,
                Type = Hl7.Fhir.Model.MeasureReport.MeasureReportType.Individual,
                Group =
                {
                    new Hl7.Fhir.Model.MeasureReport.GroupComponent
                    {
                        Population =
                        {
                            new Hl7.Fhir.Model.MeasureReport.PopulationComponent
                            {
                                ElementId = "initial-population",
                                Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "initial-population"),
                                Count = 1
                            }
                        }
                    }
                }
            };

            var serializer = new FhirJsonSerializer();
            var mrJson = serializer.SerializeToString(measureReport);
            var mrReference = $"MeasureReport/{measureReport.Id}";

            // Include the same Condition reference twice — only one should be counted
            var conditionRef = "Condition/cond-123";
            var conditionJson = "{\"resourceType\":\"Condition\",\"id\":\"cond-123\"}";

            var reportName = blobService.GetReportName(schedule);
            var entryBlobName = blobService.GetBlobName(reportName, $"patient-{patientId}-DE-111.mr");

            await UploadPatientBlobAsync(entryBlobName,
                (conditionRef, conditionJson),
                (mrReference, mrJson),
                (conditionRef, conditionJson)); // duplicate — should be skipped

            await SeedReportEntryAsync(context, schedule.Id, facilityId, patientId,
                ("DE-111", MeasureReportStatus.ReadyForValidation, measureReport.Id, entryBlobName));

            var result = await aggregator.AggregateToABS(patientId, schedule);

            Assert.NotNull(result);
            var aggregatedMr = Assert.Single(result.MeasureReportResults);

            // Condition should appear once in resource references, MeasureReport once
            Assert.Equal(2, aggregatedMr.ResourceReferences.Count);
            Assert.True(aggregatedMr.ResourceCount.ContainsKey("Condition"));
            Assert.Equal(1, aggregatedMr.ResourceCount["Condition"]);
            Assert.True(aggregatedMr.ResourceCount.ContainsKey("MeasureReport"));
            Assert.Equal(1, aggregatedMr.ResourceCount["MeasureReport"]);
        }

        // ------------------------------------------------------------------ //
        //  Mixed statuses — only ReadyForValidation entries are processed
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task AggregateToABS_MixedStatuses_OnlyProcessesReadyForValidation()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var aggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();
            var blobService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();

            var facilityId = "pat-agg-facility-005";
            var patientId = "patient-005";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            // Build a MeasureReport for the ReadyForValidation entry
            var measureReport = new Hl7.Fhir.Model.MeasureReport
            {
                Id = Guid.NewGuid().ToString(),
                Measure = "http://example.com/Measure/DE-111",
                Status = Hl7.Fhir.Model.MeasureReport.MeasureReportStatus.Complete,
                Type = Hl7.Fhir.Model.MeasureReport.MeasureReportType.Individual,
                Group =
                {
                    new Hl7.Fhir.Model.MeasureReport.GroupComponent
                    {
                        Population =
                        {
                            new Hl7.Fhir.Model.MeasureReport.PopulationComponent
                            {
                                ElementId = "initial-population",
                                Code = new CodeableConcept("http://terminology.hl7.org/CodeSystem/measure-population", "initial-population"),
                                Count = 1
                            }
                        }
                    }
                }
            };

            var serializer = new FhirJsonSerializer();
            var mrJson = serializer.SerializeToString(measureReport);
            var mrReference = $"MeasureReport/{measureReport.Id}";

            var reportName = blobService.GetReportName(schedule);
            var readyBlobName = blobService.GetBlobName(reportName, $"patient-{patientId}-DE-111.mr");

            await UploadPatientBlobAsync(readyBlobName, (mrReference, mrJson));

            // Seed entry with one ReadyForValidation and one NotReportable
            await SeedReportEntryAsync(context, schedule.Id, facilityId, patientId,
                ("DE-111", MeasureReportStatus.ReadyForValidation, measureReport.Id, readyBlobName),
                ("DE-222", MeasureReportStatus.NotReportable, null, null));

            var result = await aggregator.AggregateToABS(patientId, schedule);

            Assert.NotNull(result);
            // Only the ReadyForValidation entry should be processed
            Assert.Single(result.MeasureReportResults);
            Assert.Equal("DE-111", result.MeasureReportResults[0].ReportType);
        }
    }
}
