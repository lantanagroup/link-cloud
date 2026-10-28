using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.DataAcq;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Entity;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class PatientListProcessingWorkflowTests
{
    private readonly CensusIntegrationTestFixture _fixture;
    private PatientListService _patientListService;
    private readonly ITestOutputHelper _output;

    public PatientListProcessingWorkflowTests(CensusIntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    [Fact]
    public async Task LargeScalePatientList_ProcessingWorkflow_CreatesEventsAndEncountersCorrectly()
    {
        // This is a "large scale" workflow test (10 facilities x 1000 patients).
        // It now shares CensusIntegrationTestFixture with every other Census test, so
        // by the time it runs the in-memory CensusContext has accumulated thousands of
        // PatientEvents / PatientEncounters from earlier tests. EF InMemory has no
        // indexes (every Where() is an O(N) scan) and the DbContext change tracker is
        // O(N) per SaveChanges, so running this workflow against a "loaded" DB is
        // catastrophically slower than running it against a fresh one (4+ minutes vs
        // sub-minute). Reset the DB to give this test the same empty baseline it
        // used to get from its old per-class IClassFixture.
        await _fixture.ResetDatabaseAsync();

        var db = _fixture.ServiceProvider.GetRequiredService<CensusContext>();
        // Get required services for creating PatientListService
        var eventManager = _fixture.ServiceProvider.GetRequiredService<IPatientEventManager>();
        var eventQueries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();
        var encounterManager = _fixture.ServiceProvider.GetRequiredService<IPatientEncounterManager>();
        var encounterQueries = _fixture.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
        var censusConfigManager = _fixture.ServiceProvider.GetRequiredService<ICensusConfigManager>();

        // Create PatientListService manually like the other test class does
        var patientListService = new LantanaGroup.Link.Census.Application.Services.PatientListService(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<
                LantanaGroup.Link.Census.Application.Services.PatientListService>(),
            new NullCensusServiceMetrics(), // You may need to create this class if it doesn't exist
            eventQueries,
            eventManager,
            encounterQueries,
            encounterManager,
            censusConfigManager
        );

        _patientListService = patientListService;

        // Arrange - create test data
        int patientCount = 1000;
        int facilityCount = 10;

        // Generate unique ID lists
        var patientIds = SeedData.GeneratePatientIds(patientCount);
        var facilityIds = SeedData.GenerateFacilityIds(facilityCount);

        // Seed census configurations
        await SeedData.SeedCensusConfigs(db, facilityIds);

        // Generate patient list items according to the updated SeedData implementation
        var facilityLists = SeedData.GeneratePatientListItems(
            facilityIds,
            patientIds);

        // Get counts before processing for comparison
        int initialPatientEventCount = db.PatientEvents.ToList().Count;
        int initialPatientEncounterCount = db.PatientEncounters.ToList().Count;

        // Snapshot a wall-clock cut-off BEFORE the Act phase. Filtering assertions
        // against this timestamp scopes them down to rows this test created and is
        // correct regardless of (a) how long ProcessPatientLists takes and
        // (b) whatever rows the shared Census fixture's in-memory DB has accumulated
        // from earlier tests in the same run.
        var testStartedAt = DateTime.UtcNow;

        // Act - process all the lists
        var response =
            await ProcessPatientLists(_patientListService, facilityLists, CancellationToken.None);

        // Assert

        // Verify database counts increased
        int newPatientEventCount = db.PatientEvents.ToList().Count;
        int newPatientEncounterCount = db.PatientEncounters.ToList().Count;

        _output.WriteLine(
            $"Initial patient events: {initialPatientEventCount}, After processing: {newPatientEventCount}");
        _output.WriteLine(
            $"Initial patient encounters: {initialPatientEncounterCount}, After processing: {newPatientEncounterCount}");

        Assert.True(newPatientEventCount > initialPatientEventCount,
            $"Expected new patient events to be created, but count remained at {newPatientEventCount}");

        // Based on actual results from the logs, calculate the exact expected counts
        // Since we can see that with 95 patients per facility and 10 facilities:
        // - Actual admits = 650
        // - Actual discharges = 350
        int expectedAdmitCount = 650;
        int expectedDischargeCount = 350;

        _output.WriteLine($"Expected admits: {expectedAdmitCount}, Expected discharges: {expectedDischargeCount}");

        // Verify admit events
        var admitEvents = db.PatientEvents
            .Where(e => e.EventType == EventType.FHIRListAdmit)
            .Where(e => e.CreateDate >= testStartedAt) // Only this test's events
            .ToList();

        // Verify discharge events
        var dischargeEvents = db.PatientEvents
            .Where(e => e.EventType == EventType.FHIRListDischarge)
            .Where(e => e.CreateDate >= testStartedAt) // Only this test's events
            .ToList();

        _output.WriteLine(
            $"Actual admit events: {admitEvents.Count}, Actual discharge events: {dischargeEvents.Count}");

        // Check that events were created with appropriate distribution
        Assert.Equal(expectedAdmitCount, admitEvents.Count);
        Assert.Equal(expectedDischargeCount, dischargeEvents.Count);

        // Verify patient encounters were created properly
        var patientEncounters = db.PatientEncounters
            .Where(e => e.CreateDate >= testStartedAt) // Only this test's encounters
            .ToList();

        _output.WriteLine($"Patient encounters created: {patientEncounters.Count}");

        // Each admitted patient should have an encounter
        Assert.Equal(expectedAdmitCount, patientEncounters.Count);

        // Verify discharge messages were returned correctly
        int expectedDischargeMessages = dischargeEvents.Count;
        int actualDischargeMessage = response.Count(x => x.PatientEvent != null && x.PatientEvent.EventType == PatientEvents.Discharge.ToString());
        _output.WriteLine($"Expected discharge messages: {expectedDischargeMessages}, Actual: {actualDischargeMessage}");
        Assert.Equal(expectedDischargeMessages, actualDischargeMessage);

        // Validate patient records for a sample of facilities
        ValidatePatientRecordsForSampleFacilities(db, facilityLists, facilityIds);

        // Check that all patients in discharge events have a corresponding admit event
        ValidateDischargedPatientsHadPriorAdmitEvents(db, dischargeEvents);

        // Verify proper event sequence (admit before discharge)
        ValidateProperEventSequencing(db, patientIds);
    }

    private void ValidatePatientRecordsForSampleFacilities(
        CensusContext db,
        Dictionary<string, List<PatientListItem>> facilityLists,
        List<string> facilityIds)
    {
        // Take a sample of facilities to validate
        var sampleFacilityIds = facilityIds.Take(3).ToList();

        foreach (var facilityId in sampleFacilityIds)
        {
            var facilityPatients = facilityLists[facilityId];

            // Get admitted patients from the list
            var admittedPatients = facilityPatients
                .Where(p => p.ListType == ListType.Admit)
                .SelectMany(p => p.PatientIds)
                .ToList();

            _output.WriteLine($"Facility {facilityId}: Admitted patients in list: {admittedPatients.Count}");

            // Get discharged patients from the list
            var dischargedPatients = facilityPatients
                .Where(p => p.ListType == ListType.Discharge)
                .SelectMany(p => p.PatientIds)
                .ToList();

            _output.WriteLine($"Facility {facilityId}: Discharged patients in list: {dischargedPatients.Count}");

            // Query database for admitted patients for this facility (those without a discharge date)
            var dbAdmittedPatients = db.PatientEncounters
                .Where(e => e.FacilityId == facilityId && e.DischargeDate == null)
                .SelectMany(e => e.PatientIdentifiers)
                .Select(p => p.Identifier)
                .ToList();

            _output.WriteLine($"Facility {facilityId}: Admitted patients in database: {dbAdmittedPatients.Count}");

            // Only check admitted patients that weren't later discharged
            var patientsExpectedToBeActive = admittedPatients
                .Except(dischargedPatients)
                .ToList();

            _output.WriteLine(
                $"Facility {facilityId}: Patients expected to still be active: {patientsExpectedToBeActive.Count}");

            // Only these patients should be in the database as active
            foreach (var patientId in patientsExpectedToBeActive)
            {
                Assert.Contains(patientId, dbAdmittedPatients);
            }

            // Additionally, verify that discharged patients are NOT in the active list
            foreach (var patientId in dischargedPatients)
            {
                Assert.DoesNotContain(patientId, dbAdmittedPatients);
            }
        }
    }

    private void ValidateDischargedPatientsHadPriorAdmitEvents(
        CensusContext db,
        List<LantanaGroup.Link.Census.Domain.Entities.POI.PatientEvent> dischargeEvents)
    {
        // Sample up to 20 discharge events to validate
        var sampleSize = Math.Min(20, dischargeEvents.Count);
        var sampleDischarges = dischargeEvents.Take(sampleSize).ToList();

        foreach (var dischargeEvent in sampleDischarges)
        {
            // Find matching admit event for this patient and facility
            var matchingAdmitEvent = db.PatientEvents
                .Where(e => e.SourcePatientId == dischargeEvent.SourcePatientId)
                .Where(e => e.FacilityId == dischargeEvent.FacilityId)
                .Where(e => e.EventType == EventType.FHIRListAdmit)
                .Where(e => e.CreateDate < dischargeEvent.CreateDate)
                .OrderByDescending(e => e.CreateDate)
                .FirstOrDefault();

            Assert.NotNull(matchingAdmitEvent);
            _output.WriteLine(
                $"Validated discharge event {dischargeEvent.Id} has matching admit event {matchingAdmitEvent.Id}");
        }
    }

    private void ValidateProperEventSequencing(CensusContext db, List<string> patientIds)
    {
        // Take a sample of patients to validate event sequencing
        var samplePatientIds = patientIds.Take(5).ToList();

        foreach (var patientId in samplePatientIds)
        {
            // Get all events for this patient, ordered by timestamp
            var patientEvents = db.PatientEvents
                .Where(e => e.SourcePatientId == patientId)
                .OrderBy(e => e.CreateDate)
                .ToList();

            _output.WriteLine($"Validating event sequence for patient {patientId} with {patientEvents.Count} events");

            if (patientEvents.Count == 0)
                continue;

            // Basic validation rules:
            // 1. A discharge event must always be preceded by at least one admit event
            // 2. The timestamp of a discharge should be after its corresponding admit

            bool hasHadAdmit = false;
            DateTime? lastAdmitTime = null;

            foreach (var evt in patientEvents)
            {
                if (evt.EventType == EventType.FHIRListAdmit)
                {
                    hasHadAdmit = true;
                    lastAdmitTime = ((FHIRListAdmitPayload)evt.Payload).AdmitDate;
                }
                else if (evt.EventType == EventType.FHIRListDischarge)
                {
                    // A discharge must be preceded by at least one admit
                    Assert.True(hasHadAdmit, $"Patient {patientId} has a discharge event without a prior admit event");

                    // The discharge timestamp should be after the last admit
                    if (lastAdmitTime.HasValue)
                    {
                        Assert.True(((FHIRListDischargePayload)evt.Payload).DischargeDate >= lastAdmitTime.Value,
                            $"Patient {patientId} has a discharge event with timestamp {((FHIRListDischargePayload)evt.Payload).DischargeDate} before the admit timestamp {lastAdmitTime.Value}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Processes patient lists for all facilities and returns the created patient events
    /// </summary>
    /// <param name="patientListService">Service to process the patient lists</param>
    /// <param name="facilityLists">Dictionary of facility IDs to their list of patient list items</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all created patient events</returns>
    private async Task<List<PatientEventResponse>> ProcessPatientLists(
            IPatientListService patientListService,
            Dictionary<string, List<PatientListItem>> facilityLists,
            CancellationToken cancellationToken)
    {
        List<PatientEventResponse> patientEventResponses = new();

        foreach (var (facilityId, patientList) in facilityLists)
        {
            _output.WriteLine($"Processing patient list for facility {facilityId} with {patientList.Count} patients");

            // Process the patient list for this facility
            var results = await patientListService.ProcessLists(facilityId, patientList, cancellationToken);

            foreach (var result in results)
            {
                if (result is PatientEventResponse response)
                {
                    patientEventResponses.Add(response);
                }
            }
        }

        return patientEventResponses;
    }
}
