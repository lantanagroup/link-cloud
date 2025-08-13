using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using Census.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
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

            // Seed test config
            var facilityId = "TestFacility";
            var config = new CensusConfigEntity { FacilityID = facilityId, ScheduledTrigger = "0 0 * * *" };
            db.CensusConfigs.Add(config);
            await db.SaveChangesAsync();

            // Create 6 lists for all ListType/TimeFrame pairs
            var admitIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
            var dischargeIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), admitIds[0] }; // last discharge uses first admit id

            var lists = new List<PatientListItem>
            {
                new PatientListItem { ListType = ListType.Admit, TimeFrame = TimeFrame.LessThan24Hours, PatientIds = new List<string> { admitIds[0] } },
                new PatientListItem { ListType = ListType.Admit, TimeFrame = TimeFrame.Between24To48Hours, PatientIds = new List<string> { admitIds[1] } },
                new PatientListItem { ListType = ListType.Admit, TimeFrame = TimeFrame.MoreThan48Hours, PatientIds = new List<string> { admitIds[2] } },
                new PatientListItem { ListType = ListType.Discharge, TimeFrame = TimeFrame.LessThan24Hours, PatientIds = new List<string> { dischargeIds[0] } },
                new PatientListItem { ListType = ListType.Discharge, TimeFrame = TimeFrame.Between24To48Hours, PatientIds = new List<string> { dischargeIds[1] } },
                new PatientListItem { ListType = ListType.Discharge, TimeFrame = TimeFrame.MoreThan48Hours, PatientIds = new List<string> { dischargeIds[2] } }, // triggers discharge workflow for admitIds[0]
            };

            // Simulate workflow: process all lists
            var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<LantanaGroup.Link.Census.Application.Services.PatientListService>(),
                new NullCensusServiceMetrics(),
                eventQueries,
                eventManager,
                encounterQueries,
                encounterManager
            );

            var responses = await patientListService.ProcessLists(facilityId, lists, CancellationToken.None);

            // Assert PatientEvent for all admits and discharges
            foreach (var id in admitIds)
                Assert.Contains(db.PatientEvents, e => e.FacilityId == facilityId && e.SourcePatientId == id && e.EventType == EventType.FHIRListAdmit);
            foreach (var id in dischargeIds)
                Assert.Contains(db.PatientEvents, e => e.FacilityId == facilityId && e.SourcePatientId == id && e.EventType == EventType.FHIRListDischarge);

            // Assert PatientEncounter created and updated for admitIds[0] (discharged)
            var encounter = db.PatientEncounters.FirstOrDefault(e => e.FacilityId == facilityId && e.PatientIdentifiers.Any(p => p.Identifier == admitIds[0]));
            Assert.NotNull(encounter);
            Assert.NotNull(encounter.AdmitDate);
            Assert.NotNull(encounter.DischargeDate);

            // Assert PatientEventResponse for discharge of admitIds[0]
            Assert.Contains(responses, r => r is PatientEventResponse resp && resp.PatientEvent?.PatientId == admitIds[0] && resp.PatientEvent?.EventType == "Discharge");
        }
    }
}
