using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census
{
    [Collection("IntegrationTests")]
    public class CernerPatientsAcquiredWorkflowTests
    {
        private readonly CensusIntegrationTestFixture _fixture;

        public CernerPatientsAcquiredWorkflowTests(CensusIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CernerPatientsAcquired_Admit()
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

            // Create Kafka Event
            var cernerPatientsAcquiredEvent = new CernerPatientsAcquired()
            {
                PatientEncounters = new List<CernerEncounters>() {
                    new CernerEncounters() {
                        PatientId = "pat-1",
                        EncounterId = "enc-1",
                        FinNumber = "fin-1",
                        MRN = "00049816311",
                        EncounterStatus = "arrived",
                        EncounterType = "ADMS",
                        AdmitDate = new DateTime(2026, 02, 02)
                    }
                }
            };

            var cernerListService = new CernerListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventManager, eventQueries, encounterQueries, encounterManager, censusConfigManager);

            var responses = await cernerListService.ProcessAdmits(facilityId, cernerPatientsAcquiredEvent, CancellationToken.None);

            Assert.Single(responses);
            Assert.Equal(PatientEvents.Admit.ToString(), ((PatientEventResponse)responses.First()).PatientEvent.EventType);
            Assert.Equal("pat-1", ((PatientEventResponse)responses.First()).PatientEvent.PatientId);
            Assert.Equal(facilityId, ((PatientEventResponse)responses.First()).FacilityId);
        }

        [Fact]
        public async Task CernerPatientsAcquired_Discharge()
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

            // Seed test data
            var patientEncounter = new PatientEncounter()
            {
                CorrelationId = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                MedicalRecordNumber = "00049816311",
                AdmitDate = new DateTime(2026, 2, 1),
                DischargeDate = null
            };

            db.PatientEncounters.Add(patientEncounter);

            db.PatientIdentifiers.Add(new PatientIdentifier()
            {
                Id = Guid.NewGuid().ToString(),
                Identifier = "pat-1",
                PatientEncounterId = patientEncounter.Id,
                SourceType = SourceType.SFTP_FHIR.ToString(),
                CreateDate = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            //NOTE: Was getting the following without this line: ' The instance of entity type 'PatientEncounter' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked. When attaching existing entities, ensure that only one entity instance with a given key value is attached. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the conflicting key values.' Found 
            db.Entry(patientEncounter).State = EntityState.Detached;

            // Create Kafka Event
            var cernerListService = new CernerListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventManager, eventQueries, encounterQueries, encounterManager, censusConfigManager);


            var dischargeEvent = new CernerPatientsAcquired()
            {
                PatientEncounters = new List<CernerEncounters>()
            };

            var dischargeResponses = await cernerListService.ProcessDischarges(facilityId, dischargeEvent, CancellationToken.None);

            Assert.Single(dischargeResponses);
            Assert.Equal(PatientEvents.Discharge.ToString(), ((PatientEventResponse)dischargeResponses.First()).PatientEvent.EventType);
            Assert.Equal("pat-1", ((PatientEventResponse)dischargeResponses.First()).PatientEvent.PatientId);
            Assert.Equal(facilityId, ((PatientEventResponse)dischargeResponses.First()).FacilityId);
        }

        [Fact]
        public async Task CernerPatientsAcquired_Update()
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

            // Seed test data 
            var patientEncounter = new PatientEncounter()
            {
                CorrelationId = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                MedicalRecordNumber = "00049816311",
                AdmitDate = new DateTime(2026, 2, 1),
                EncounterStatus = "arrived",
                DischargeDate = null
            };

            db.PatientEncounters.Add(patientEncounter);

            db.PatientIdentifiers.Add(new PatientIdentifier()
            {
                Id = Guid.NewGuid().ToString(),
                Identifier = "pat-1",
                PatientEncounterId = patientEncounter.Id,
                SourceType = SourceType.SFTP_FHIR.ToString(),
                CreateDate = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            //NOTE: Was getting the following without this line: ' The instance of entity type 'PatientEncounter' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked. When attaching existing entities, ensure that only one entity instance with a given key value is attached. Consider using 'DbContextOptionsBuilder.EnableSensitiveDataLogging' to see the conflicting key values.' Found 
            db.Entry(patientEncounter).State = EntityState.Detached;

            // Create Kafka Event
            var cernerListService = new CernerListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventManager, eventQueries, encounterQueries, encounterManager, censusConfigManager);


            // Create Kafka Event
            var cernerPatientsAcquiredEvent = new CernerPatientsAcquired()
            {
                PatientEncounters = new List<CernerEncounters>() {
                    new CernerEncounters() {
                        PatientId = "pat-1",
                        EncounterId = "enc-1",
                        FinNumber = "fin-1",
                        MRN = "00049816311",
                        EncounterStatus = "on-leave",
                        EncounterType = "ADMS",
                        AdmitDate = new DateTime(2026, 02, 02)
                    }
                }
            };

            var processedResponses = await cernerListService.ProcessAdmits(facilityId, cernerPatientsAcquiredEvent, CancellationToken.None);

            Assert.Single(processedResponses);
            Assert.Equal(PatientEvents.Update.ToString(), ((PatientEventResponse)processedResponses.First()).PatientEvent.EventType);
            Assert.Equal("pat-1", ((PatientEventResponse)processedResponses.First()).PatientEvent.PatientId);
            Assert.Equal(facilityId, ((PatientEventResponse)processedResponses.First()).FacilityId);
        }
    }
}
