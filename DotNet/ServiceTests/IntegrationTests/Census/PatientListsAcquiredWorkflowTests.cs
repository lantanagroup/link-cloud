using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using Census.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census
{
    [Collection("CensusIntegrationTests")]
    public class PatientListsAcquiredWorkflowTests
    {
        private readonly CensusIntegrationTestFixture _fixture;

        public PatientListsAcquiredWorkflowTests(CensusIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task PatientListsAcquired_AdmitAndDischarge_Workflow_CreatesEventsAndEncounters()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CensusContext>();
            var configManager = scope.ServiceProvider.GetRequiredService<ICensusConfigManager>();
            var eventManager = scope.ServiceProvider.GetRequiredService<IPatientEventManager>();
            var eventQueries = scope.ServiceProvider.GetRequiredService<IPatientEventQueries>();
            var encounterManager = scope.ServiceProvider.GetRequiredService<IPatientEncounterManager>();
            var encounterQueries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
            var censusConfigManager = scope.ServiceProvider.GetRequiredService<ICensusConfigManager>();

            // Seed test config
            var facilityId = "TestFacility" + Guid.NewGuid().ToString();
            var config = new CensusConfigEntity { FacilityID = facilityId, ScheduledTrigger = "0 0 * * *" };
            db.CensusConfigs.Add(config);
            await db.SaveChangesAsync();

            // Create 6 lists for all ListType/TimeFrame pairs
            var admitIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
            var dischargeIds = new[]
            {
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), admitIds[0]
            }; // last discharge uses first admit id

            var lists = new List<PatientListItem>
            {
                new PatientListItem
                {
                    ListType = ListType.Admit, TimeFrame = TimeFrame.LessThan24Hours,
                    PatientIds = new List<string> { admitIds[0] }
                },
                new PatientListItem
                {
                    ListType = ListType.Admit, TimeFrame = TimeFrame.Between24To48Hours,
                    PatientIds = new List<string> { admitIds[1] }
                },
                new PatientListItem
                {
                    ListType = ListType.Admit, TimeFrame = TimeFrame.MoreThan48Hours,
                    PatientIds = new List<string> { admitIds[2] }
                },
                new PatientListItem
                {
                    ListType = ListType.Discharge, TimeFrame = TimeFrame.LessThan24Hours,
                    PatientIds = new List<string> { dischargeIds[0] }
                },
                new PatientListItem
                {
                    ListType = ListType.Discharge, TimeFrame = TimeFrame.Between24To48Hours,
                    PatientIds = new List<string> { dischargeIds[1] }
                },
                new PatientListItem
                {
                    ListType = ListType.Discharge, TimeFrame = TimeFrame.MoreThan48Hours,
                    PatientIds = new List<string> { dischargeIds[2] }
                }, // triggers discharge workflow for admitIds[0]
            };

            // Simulate workflow: process all lists
            var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventQueries,
                eventManager,
                encounterQueries,
                encounterManager,
                censusConfigManager
            );

            var responses = await patientListService.ProcessLists(facilityId, lists, CancellationToken.None);

            // Assert PatientEvent for all admits and discharges
            foreach (var id in admitIds)
                Assert.Contains(db.PatientEvents,
                    e => e.FacilityId == facilityId && e.SourcePatientId == id &&
                         e.EventType == EventType.FHIRListAdmit);
            foreach (var id in dischargeIds)
                Assert.Contains(db.PatientEvents,
                    e => e.FacilityId == facilityId && e.SourcePatientId == id &&
                         e.EventType == EventType.FHIRListDischarge);

            // Assert PatientEncounter created and updated for admitIds[0] (discharged)
            var encounter = db.PatientEncounters.FirstOrDefault(e =>
                e.FacilityId == facilityId && e.PatientIdentifiers.Any(p => p.Identifier == admitIds[0]));
            Assert.NotNull(encounter);
            Assert.NotNull(encounter.AdmitDate);
            Assert.NotNull(encounter.DischargeDate);

            // Assert PatientEventResponse for discharge of admitIds[0]
            Assert.Contains(responses,
                r => r is PatientEventResponse resp && resp.PatientEvent?.PatientId == admitIds[0] &&
                     resp.PatientEvent?.EventType == "Discharge");
        }

        [Fact]
        public async Task ProcessList_WithValidSingleList_CreatesEventsCorrectly()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CensusContext>();
            var eventManager = scope.ServiceProvider.GetRequiredService<IPatientEventManager>();
            var eventQueries = scope.ServiceProvider.GetRequiredService<IPatientEventQueries>();
            var encounterManager = scope.ServiceProvider.GetRequiredService<IPatientEncounterManager>();
            var encounterQueries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
            var censusConfigManager = scope.ServiceProvider.GetRequiredService<ICensusConfigManager>();
            
            // Seed test config
            var facilityId = "TestFacility" + Guid.NewGuid().ToString();
            var config = new CensusConfigEntity { FacilityID = facilityId, ScheduledTrigger = "0 0 * * *" };
            db.CensusConfigs.Add(config);
            await db.SaveChangesAsync();

            // Create a single list
            var patientId = Guid.NewGuid().ToString();
            var list = new PatientListItem
            {
                ListType = ListType.Admit,
                TimeFrame = TimeFrame.LessThan24Hours,
                PatientIds = new List<string> { patientId }
            };

            // Create service and process
            var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventQueries,
                eventManager,
                encounterQueries,
                encounterManager,
                censusConfigManager
            );

            var responses = await patientListService.ProcessList(facilityId, list, CancellationToken.None);

            // Assertions
            Assert.Empty(responses);
            Assert.Contains(db.PatientEvents,
                e => e.FacilityId == facilityId && e.SourcePatientId == patientId &&
                     e.EventType == EventType.FHIRListAdmit);

            // Verify encounter was created
            var encounter = db.PatientEncounters.FirstOrDefault(e =>
                e.FacilityId == facilityId && e.PatientIdentifiers.Any(p => p.Identifier == patientId));
            Assert.NotNull(encounter);
            Assert.NotNull(encounter.AdmitDate);
            Assert.Null(encounter.DischargeDate); // Should be null since this is just an admit
        }

        [Fact]
        public async Task ProcessList_WithDuplicateProcessing_ShouldSkipAndReturnAppropriateResponse()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CensusContext>();
            var eventManager = scope.ServiceProvider.GetRequiredService<IPatientEventManager>();
            var eventQueries = scope.ServiceProvider.GetRequiredService<IPatientEventQueries>();
            var encounterManager = scope.ServiceProvider.GetRequiredService<IPatientEncounterManager>();
            var encounterQueries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
            var censusConfigManager = scope.ServiceProvider.GetRequiredService<ICensusConfigManager>();

            // Seed test config
            var facilityId = "TestFacility" + Guid.NewGuid().ToString();
            var config = new CensusConfigEntity { FacilityID = facilityId, ScheduledTrigger = "0 0 * * *" };
            db.CensusConfigs.Add(config);
            await db.SaveChangesAsync();

            // Create test data
            var patientId = Guid.NewGuid().ToString();
            var list = new PatientListItem
            {
                ListType = ListType.Admit,
                TimeFrame = TimeFrame.LessThan24Hours,
                PatientIds = new List<string> { patientId }
            };

            var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventQueries,
                eventManager,
                encounterQueries,
                encounterManager,
                censusConfigManager
            );

            // First processing
            var firstResponses = await patientListService.ProcessList(facilityId, list, CancellationToken.None);

            // Second processing (should be skipped for the same patient)
            var secondResponses = await patientListService.ProcessList(facilityId, list, CancellationToken.None);

            // Assert
            Assert.Empty(firstResponses);
            Assert.Empty(secondResponses);

            // Check that there's only one event in the database (not duplicated)
            var events = db.PatientEvents.Where(e => e.FacilityId == facilityId && e.SourcePatientId == patientId)
                .ToList();
            Assert.Single(events);
        }

        // [Fact]
        // public async Task ProcessLists_WithEmptyPatientIds_ShouldReturnEmptyResponseList()
        // {
        //     using var scope = _fixture.ServiceProvider.CreateScope();
        //     var db = scope.ServiceProvider.GetRequiredService<CensusContext>();
        //     var eventManager = scope.ServiceProvider.GetRequiredService<IPatientEventManager>();
        //     var eventQueries = scope.ServiceProvider.GetRequiredService<IPatientEventQueries>();
        //     var encounterManager = scope.ServiceProvider.GetRequiredService<IPatientEncounterManager>();
        //     var encounterQueries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
        //     var censusConfigManager = scope.ServiceProvider.GetRequiredService<ICensusConfigManager>();
        //
        //     // Seed test config
        //     var facilityId = "TestFacility";
        //     var config = new CensusConfigEntity { FacilityID = facilityId, ScheduledTrigger = "0 0 * * *" };
        //     db.CensusConfigs.Add(config);
        //     await db.SaveChangesAsync();
        //
        //     // Create empty list
        //     var emptyList = new PatientListItem
        //     {
        //         ListType = ListType.Admit,
        //         TimeFrame = TimeFrame.LessThan24Hours,
        //         PatientIds = new List<string>() // Empty list
        //     };
        //
        //     var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
        //         new Microsoft.Extensions.Logging.Abstractions.NullLogger<
        //             LantanaGroup.Link.Census.Application.Services.PatientListService>(),
        //         new NullCensusServiceMetrics(),
        //         eventQueries,
        //         eventManager,
        //         encounterQueries,
        //         encounterManager,
        //         censusConfigManager
        //     );
        //
        //     var responses = await patientListService.ProcessList(facilityId, emptyList, CancellationToken.None);
        //
        //     // Assert
        //     Assert.Empty(responses);
        // }

        [Fact]
        public async Task ProcessLists_WithCancellationToken_ShouldHonorCancellation()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CensusContext>();
            var eventManager = scope.ServiceProvider.GetRequiredService<IPatientEventManager>();
            var eventQueries = scope.ServiceProvider.GetRequiredService<IPatientEventQueries>();
            var encounterManager = scope.ServiceProvider.GetRequiredService<IPatientEncounterManager>();
            var encounterQueries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
            var censusConfigManager = scope.ServiceProvider.GetRequiredService<ICensusConfigManager>();

            // Seed test config
            var facilityId = "TestFacility" + Guid.NewGuid().ToString();
            var config = new CensusConfigEntity { FacilityID = facilityId, ScheduledTrigger = "0 0 * * *" };
            db.CensusConfigs.Add(config);
            await db.SaveChangesAsync();

            // Create a large list that will take some time to process
            var patientIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid().ToString()).ToList();
            var list = new PatientListItem
            {
                ListType = ListType.Admit,
                TimeFrame = TimeFrame.LessThan24Hours,
                PatientIds = patientIds
            };

            // Create a cancellation token and cancel it immediately
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventQueries,
                eventManager,
                encounterQueries,
                encounterManager,
                censusConfigManager
            );

            // Should throw OperationCanceledException
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await patientListService.ProcessList(facilityId, list, cts.Token)
            );

            // Verify no patient events were created
            Assert.Empty(db.PatientEvents.Where(e =>
                e.FacilityId == facilityId && patientIds.Contains(e.SourcePatientId)));
        }

        [Fact]
        public async Task ProcessList_WithInvalidFacilityId_ShouldHandleErrorGracefully()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CensusContext>();
            var eventManager = scope.ServiceProvider.GetRequiredService<IPatientEventManager>();
            var eventQueries = scope.ServiceProvider.GetRequiredService<IPatientEventQueries>();
            var encounterManager = scope.ServiceProvider.GetRequiredService<IPatientEncounterManager>();
            var encounterQueries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
            var censusConfigManager = scope.ServiceProvider.GetRequiredService<ICensusConfigManager>();

            // Use a facility ID that doesn't exist in the database
            var invalidFacilityId = "NonExistentFacility";

            var patientId = Guid.NewGuid().ToString();
            var list = new PatientListItem
            {
                ListType = ListType.Admit,
                TimeFrame = TimeFrame.LessThan24Hours,
                PatientIds = new List<string> { patientId }
            };

            var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventQueries,
                eventManager,
                encounterQueries,
                encounterManager,
                censusConfigManager
            );

            // Assert that an ArgumentException is thrown when processing with an invalid facility ID
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await patientListService.ProcessList(invalidFacilityId, list, CancellationToken.None)
            );

        }
    }
}