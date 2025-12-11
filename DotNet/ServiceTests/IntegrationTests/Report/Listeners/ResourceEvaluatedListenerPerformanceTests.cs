using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Entities.Enums;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Moq;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    public class ResourceEvaluatedListenerPerformanceTests
    {
        private readonly ReportIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly IServiceScopeFactory _scopeFactory;

        public ResourceEvaluatedListenerPerformanceTests(ReportIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        }

        [Fact(Skip = "Manual performance test only - run locally as needed")]
        //[Fact]
        public async Task Performance_Test_ResourceEvaluated_Listener()
        {
            // Reset mocks
            _fixture.ResetMocks();

            var dbContext = _fixture.ServiceProvider.GetRequiredService<MongoDbContext>();
            await dbContext.EnsureIndexesAsync();

            // Setup blob storage to return a dummy URI
            ReportIntegrationTestFixture.BlobStorageMock.Setup(b => b.UploadAsync(It.IsAny<ReportSchedule>(), It.IsAny<PatientSubmissionModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Uri("https://test.blob/uri"));

            // Arrange: Create shared resources
            var sharedResources = new List<Resource>();
            for (int j = 0; j < 5000; j++)
            {
                var loc = new Location
                {
                    Id = $"loc-{j}"
                };
                sharedResources.Add(loc);
            }

            // Create one schedule with two report types
            var facilityId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid().ToString();

            var reportTypes = new List<string> { "ReportType1", "ReportType2", "ReportType3", "ReportType4", "ReportType5" };
            var patients = new List<string> { "Patient1", "Patient2", "Patient3", "Patient4", "Patient5" };

            var schedule = new ReportSchedule
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = reportTypes,
                Frequency = Frequency.Monthly,
                CreateDate = DateTime.UtcNow
            };

            var entries = new List<PatientSubmissionEntry>();
            for (int i = 0; i < patients.Count; i++)
            {
                var entry = new PatientSubmissionEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = facilityId,
                    ReportScheduleId = schedule.Id,
                    PatientId = patients[i],
                    ReportType = reportTypes[i],
                    Status = PatientSubmissionStatus.PendingEvaluation,
                    ValidationStatus = ValidationStatus.Pending,
                    CreateDate = DateTime.UtcNow
                };
                entries.Add(entry);
            }

            // Add to database
            using var scope = _scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            await database.ReportScheduledRepository.AddAsync(schedule);
            foreach (var entry in entries)
            {
                await database.SubmissionEntryRepository.AddAsync(entry);
            }
            await database.SaveChangesAsync();

            // Create listener
            var listener = _fixture.ServiceProvider.GetRequiredService<ResourceEvaluatedListener>();

            List<(string, long)> resourceRunTimes = new List<(string, long)>();
            List<(string, long)> measureReportRunTimes = new List<(string, long)>();

            Dictionary<string, long> patientCompletionTime = new Dictionary<string, long>();

            // Act: Process for each patient/report type
            for (int index = 0; index < 2/*patients.Count*/; index++)
            {
                var patientStopWatch = new Stopwatch();
                var patientId = patients[index];
                var reportType = reportTypes[index];
                patientCompletionTime.Add(patientId, 0);
                patientStopWatch.Start();

                // Create patient-specific resources
                var patientResources = new List<Resource>();
                for (int j = 0; j < 5000; j++)
                {
                    var obs = new Observation
                    {
                        Id = $"obs-{patientId}-{j}",
                        Status = ObservationStatus.Final
                    };
                    patientResources.Add(obs);
                }

                // All resources for this patient: patient-specific + shared
                var allResources = patientResources.Concat(sharedResources).ToList();

                // Create MeasureReport with EvaluatedResources
                var mr = new MeasureReport
                {
                    Id = $"mr-{patientId}",
                    Measure = "http://example.org/Measure/ExampleMeasure"
                };
                foreach (var res in allResources)
                {
                    var resRef = new ResourceReference($"{res.TypeName}/{res.Id}");
                    mr.EvaluatedResource.Add(resRef);
                }

                // Serialize and process MeasureReport first
                var serializer = new FhirJsonSerializer();
                var mrJson = serializer.SerializeToString(mr);
                var mrElem = JsonDocument.Parse(mrJson).RootElement;

                var mrConsumeResult = CreateConsumeResult(facilityId, schedule.Id, patientId, reportType, mrElem, true, correlationId);

                var mrSw = Stopwatch.StartNew();
                await listener.ProcessMessageAsync(mrConsumeResult, default);
                mrSw.Stop();

                measureReportRunTimes.Add((mr.TypeName, mrSw.ElapsedMilliseconds));

                // Process all resources
                for (int j = 0; j < allResources.Count; j++)
                {
                    var res = allResources[j];
                    var jsonStr = serializer.SerializeToString(res);
                    var elem = JsonDocument.Parse(jsonStr).RootElement;

                    var consumeResult = CreateConsumeResult(facilityId, schedule.Id, patientId, reportType, elem, true, correlationId);

                    var sw = Stopwatch.StartNew();
                    await listener.ProcessMessageAsync(consumeResult, default);
                    sw.Stop();

                    resourceRunTimes.Add((res.TypeName, sw.ElapsedMilliseconds));
                }

                patientStopWatch.Stop();
                patientCompletionTime[patientId] = patientStopWatch.ElapsedMilliseconds;
            }

            var resourceAverage = resourceRunTimes.Average(i => i.Item2);
            var resourceMin = resourceRunTimes.MinBy(i => i.Item2);
            var resourceMax = resourceRunTimes.MaxBy(i => i.Item2);
            _output.WriteLine($"Average Resource (non-MR) Processing Time: {resourceAverage} ms");
            _output.WriteLine($"Minimum Resource (non-MR) Processing Time: {resourceMin.Item1}: {resourceMin.Item2} ms");
            _output.WriteLine($"Maximum Resource (non-MR) Processing Time: {resourceMax.Item1}: {resourceMax.Item2} ms");

            var mrAverage = measureReportRunTimes.Average(i => i.Item2);
            var mrMin = measureReportRunTimes.MinBy(i => i.Item2);
            var mrMax = measureReportRunTimes.MaxBy(i => i.Item2);
            _output.WriteLine($"Average MeasureReport Processing Time: {mrAverage} ms");
            _output.WriteLine($"Minimum MeasureReport Processing Time: {mrMin.Item1}: {mrMin.Item2} ms");
            _output.WriteLine($"Maximum MeasureReport Processing Time: {mrMax.Item1}: {mrMax.Item2} ms");

            var totalTime = (long)0;
            for (int index = 0; index < patients.Count; index++)
            {
                totalTime += patientCompletionTime[patients[index]];
                _output.WriteLine($"Patient {patients[index]} Processing Time: {patientCompletionTime[patients[index]]} ms");
            }

            _output.WriteLine($"Total Processing Time: {totalTime} ms");

            using var assertScope = _scopeFactory.CreateScope();
            var queries = assertScope.ServiceProvider.GetRequiredService<ISubmissionEntryQueries>();

            int idx = 1;
            foreach (var patient in patients)
            {
                var bundler = _fixture.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>();

                var getDataStopWatch = new Stopwatch();
                getDataStopWatch.Start();
                var patientReportData = await queries.GetPatientReportData(facilityId, schedule.Id, patient, cancellationToken: CancellationToken.None);
                getDataStopWatch.Stop();

                _output.WriteLine($"Patient {patient} GetPatientReportData Time: {getDataStopWatch.ElapsedMilliseconds} ms");

                var bundleStopWatch = new Stopwatch();
                bundleStopWatch.Start();
                var model = await bundler.GenerateBundle(patientReportData, facilityId, patient, schedule.Id);
                bundleStopWatch.Stop();

                _output.WriteLine($"Patient {patient} GenerateBundle Time: {bundleStopWatch.ElapsedMilliseconds} ms");

                Assert.Equal(10000, patientReportData.ReportData["ReportType" + idx.ToString()].Resources.Count); //10k Resources
                Assert.Equal(10001, model.Bundle.Entry.Count()); //10k resources + 1 Measure Report
                idx++;
            }


            // Assertion phase: After all processing is complete
            for (idx = 0; idx < patients.Count; idx++)
            {
                var patientId = patients[idx];
                var reportType = reportTypes[idx];
                await AssertPatientDataAsync(facilityId, schedule.Id, patientId, reportType);
            }
        }

        private async Task AssertPatientDataAsync(string facilityId, string scheduleId, string patientId, string reportType)
        {
            using var assertScope = _scopeFactory.CreateScope();
            var mongoDb = assertScope.ServiceProvider.GetRequiredService<IMongoDatabase>();
            var db = assertScope.ServiceProvider.GetRequiredService<IDatabase>();

            var resourceColl = mongoDb.GetCollection<FhirResource>("fhirResource");

            var obsFilter = Builders<FhirResource>.Filter.Eq(r => r.FacilityId, facilityId) &
                            Builders<FhirResource>.Filter.Eq(r => r.PatientId, patientId) &
                            Builders<FhirResource>.Filter.Eq(r => r.ResourceType, "Observation");

            var obsCount = await resourceColl.CountDocumentsAsync(obsFilter);
            Assert.Equal(5000L, obsCount);

            var locFilter = Builders<FhirResource>.Filter.Eq(r => r.FacilityId, facilityId) &
                            Builders<FhirResource>.Filter.Eq(r => r.ResourceType, "Location");

            var locCount = await resourceColl.CountDocumentsAsync(locFilter);
            Assert.Equal(5000L, locCount);

            var entry = await db.SubmissionEntryRepository.SingleAsync(e => e.PatientId == patientId && e.ReportScheduleId == scheduleId && e.ReportType == reportType);

            var mapColl = mongoDb.GetCollection<PatientSubmissionEntryResourceMap>("patientSubmissionEntryResourceMap");
            var mapFilter = Builders<PatientSubmissionEntryResourceMap>.Filter.Eq(m => m.SubmissionEntryId, entry.Id) &
                            Builders<PatientSubmissionEntryResourceMap>.Filter.Eq(m => m.ReportTypes, new List<string> { reportType });

            var mapCount = await mapColl.CountDocumentsAsync(mapFilter);
            Assert.Equal(10000L, mapCount);

            // Verify GetPatientReportData returns correct data
            var queries = assertScope.ServiceProvider.GetRequiredService<ISubmissionEntryQueries>();
            var patientData = await queries.GetPatientReportData(facilityId, scheduleId, patientId, null, CancellationToken.None);
            Assert.True(patientData.ReportData.ContainsKey(reportType));
            var reportData = patientData.ReportData[reportType];
            Assert.Equal(10000, reportData.Resources.Count);

            // Check types in the returned data (load resources to count types)
            var resourceTypes = await Task.WhenAll(reportData.Resources.Select(async m =>
            {
                var fhirResource = await db.ResourceRepository.GetAsync(m.Id);
                return fhirResource?.ResourceType ?? string.Empty;
            }));

            var returnedObsCount = resourceTypes.Count(t => t == "Observation");
            var returnedLocCount = resourceTypes.Count(t => t == "Location");
            Assert.Equal(5000, returnedObsCount);
            Assert.Equal(5000, returnedLocCount);
        }

        private ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> CreateConsumeResult(string facilityId, string reportTrackingId, string patientId, string reportType, JsonElement resourceElement, bool isReportable, string correlationId)
        {
            var headers = new Headers();
            headers.Add("X-Correlation-Id", Encoding.UTF8.GetBytes(correlationId));

            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = facilityId },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = reportTrackingId,
                    PatientId = patientId,
                    ReportType = reportType,
                    Resource = resourceElement,
                    IsReportable = isReportable
                },
                Headers = headers
            };

            return new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };
        }
    }
}