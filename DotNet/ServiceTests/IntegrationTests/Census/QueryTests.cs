using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

[Collection("CensusIntegrationTests")]
public class QueryTests
{
    private readonly CensusIntegrationTestFixture _fixture;

    public QueryTests(CensusIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region PatientEventQueries Tests

    [Fact]
    public async Task GetLatestEventByFacilityAndPatientId_ReturnsLatestEvent()
    {
        // Arrange
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var patientId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();
        var newCorreltationId = Guid.NewGuid().ToString();

        // Add multiple events for the same patient, with different dates
        var olderAdmitPayload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow.AddDays(-2));
        var olderAdmitEvent = olderAdmitPayload.CreatePatientEvent(facilityId, correlationId);

        var olderDischargePayload = new FHIRListDischargePayload(patientId, DateTime.UtcNow.AddDays(-1));
        var olderDischargeEvent = olderDischargePayload.CreatePatientEvent(facilityId, correlationId);

        var newerPayload = new FHIRListAdmitPayload(patientId, DateTime.UtcNow);
        var newerEvent = newerPayload.CreatePatientEvent(facilityId, newCorreltationId);

        await db.PatientEvents.AddRangeAsync(olderAdmitEvent, olderDischargeEvent, newerEvent);
        await db.SaveChangesAsync();

        // Act
        var result = await queries.GetLatestEventByFacilityAndPatientId(facilityId, patientId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(newerEvent.Id, result.Id);
        Assert.Equal(EventType.FHIRListAdmit, result.EventType);
        Assert.Equal(newCorreltationId, result.CorrelationId);
    }

    [Fact]
    public async Task GetLatestEventByFacilityAndPatientId_WithInvalidParameters_ThrowsArgumentException()
    {
        // Arrange
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();

        // Act & Assert - Invalid facility ID
        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetLatestEventByFacilityAndPatientId(null, "validPatientId", CancellationToken.None));

        // Act & Assert - Invalid patient ID
        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetLatestEventByFacilityAndPatientId("validFacilityId", null, CancellationToken.None));
    }

    [Fact]
    public async Task GetPatientEvents_ReturnsFilteredEvents()
    {
        // Arrange
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow.AddDays(-5);
        var endDate = DateTime.UtcNow;

        //matching all criteria
        var validEventPayload = new FHIRListAdmitPayload(Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(-3));
        var validEvent = validEventPayload.CreatePatientEvent(facilityId, correlationId);

        //different facility
        var otherFacilityPayload = new FHIRListAdmitPayload(Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(-2));
        var otherFacilityEvent = otherFacilityPayload.CreatePatientEvent("OtherFacility", Guid.NewGuid().ToString());

        //different correlation ID
        var otherCorrelationPayload = new FHIRListAdmitPayload(Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(-1));
        var otherCorrelationEvent = otherCorrelationPayload.CreatePatientEvent(facilityId, Guid.NewGuid().ToString());

        //outside date range
        var outsideDatePayload = new FHIRListAdmitPayload(Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(-10));
        var outsideDateEvent = outsideDatePayload.CreatePatientEvent(facilityId, correlationId);

        // Create events with different parameters
        var events = new List<PatientEvent>
        {
            // Matching all criteria
            validEvent,
            // Different facility
            otherFacilityEvent,
            // Different correlation ID
            otherCorrelationEvent,
            // Outside date range
            outsideDateEvent
        };

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        // Act
        var allResults = await queries.GetPatientEvents(facilityId, cancellationToken: CancellationToken.None);
        var filteredByCorrelation =
            await queries.GetPatientEvents(facilityId, correlationId, cancellationToken: CancellationToken.None);
        var filteredByDateRange = await queries.GetPatientEvents(
            facilityId,
            correlationId: null,
            startDate: startDate,
            endDate: endDate,
            cancellationToken: CancellationToken.None);
        var fullyFiltered = await queries.GetPatientEvents(
            facilityId,
            correlationId,
            startDate,
            endDate,
            CancellationToken.None);

        // Assert
        Assert.Equal(3, allResults.Count(e => e.FacilityId == facilityId));
        Assert.Equal(2, filteredByCorrelation.Count()); // 2 events with matching facility and correlation ID
        Assert.Equal(2, filteredByDateRange.Count()); // 3 events within date range for facility
        Assert.Single(fullyFiltered); // 1 event matching all criteria
    }

    [Fact]
    public async Task DeletePatientEventByCorrelationId_DeletesMatchingEvents()
    {
        // Arrange
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();

        // Reset database to ensure clean state
        await _fixture.ResetDatabaseAsync();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = "TestFacility" + Guid.NewGuid().ToString();

        var patient1Payload = new FHIRListAdmitPayload(Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(-3));
        var patient1Event = patient1Payload.CreatePatientEvent(facilityId, correlationId);

        var patient2Payload = new FHIRListAdmitPayload(Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(-2));
        var patient2Event = patient2Payload.CreatePatientEvent(facilityId, correlationId);

        var events = new List<PatientEvent>
        {
            patient1Event,
            patient2Event
        };

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        // Verify events exist
        var initialCount = await db.PatientEvents.CountAsync();
        Assert.Equal(2, initialCount);

        // Act
        await queries.DeletePatientEventByCorrelationId(correlationId, CancellationToken.None);

        // Assert
        var remainingEvents = await db.PatientEvents.ToListAsync();
        Assert.Empty(remainingEvents);
    }

    [Fact]
    public async Task DeletePatientEventByCorrelationId_WithInvalidCorrelationId_ThrowsArgumentException()
    {
        // Arrange
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.DeletePatientEventByCorrelationId(null, CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.DeletePatientEventByCorrelationId("", CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.DeletePatientEventByCorrelationId("   ", CancellationToken.None));
    }

    #endregion

    #region PatientEncounterQueries Tests

    [Fact]
    public async Task GetPatientEncounterByCorrelationIdAsync_ReturnsCorrectEncounter()
    {
        // Arrange
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var patientId = Guid.NewGuid().ToString();

        var encounter = new PatientEncounter
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            AdmitDate = DateTime.UtcNow.AddDays(-5),
            DischargeDate = null,
            PatientIdentifiers = new List<PatientIdentifier>
            {
                new PatientIdentifier
                {
                    Id = Guid.NewGuid().ToString(),
                    Identifier = patientId,
                    SourceType = SourceType.FHIR.ToString()
                }
            }
        };

        await db.PatientEncounters.AddAsync(encounter);
        await db.SaveChangesAsync();

        // Act
        var result = await queries.GetPatientEncounterByCorrelationIdAsync(correlationId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(encounter.Id, result.Id);
        Assert.Equal(facilityId, result.FacilityId);
        Assert.Equal(correlationId, result.CorrelationId);
        Assert.NotNull(result.PatientIdentifiers);
        Assert.Single(result.PatientIdentifiers);
        Assert.Equal(patientId, result.PatientIdentifiers.First().Identifier);
    }

    [Fact]
    public async Task GetPatientEncounterByCorrelationIdAsync_WithInvalidCorrelationId_ThrowsArgumentException()
    {
        // Arrange
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetPatientEncounterByCorrelationIdAsync(null, CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetPatientEncounterByCorrelationIdAsync("", CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetPatientEncounterByCorrelationIdAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task GetAdmittedPatientEventModelsByDateRange_ReturnsCorrectEvents()
    {
        // Arrange
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var patientId1 = Guid.NewGuid().ToString();
        var patientId2 = Guid.NewGuid().ToString();

        // Create test start and end dates
        var startDate = DateTime.UtcNow.AddDays(-5);
        var endDate = DateTime.UtcNow;

        // Create the events and their payloads
        var events = new List<PatientEvent>();
        // Within date range, admit event, patient 1
        var admitCorrelationId1 = Guid.NewGuid().ToString();
        var patient1AdmitPayload = new FHIRListAdmitPayload(patientId1, DateTime.UtcNow.AddDays(-3));
        var patient1AdmitEvent = patient1AdmitPayload.CreatePatientEvent(facilityId, admitCorrelationId1);
        events.Add(patient1AdmitEvent);

        // Within date range, discharge event, patient 1 (latest for patient 1)
        //var dischargeCorrelationId1 = Guid.NewGuid().ToString();
        //var patient1DischargePayload = new FHIRListDischargePayload(patientId1, DateTime.UtcNow.AddDays(-1));
        //var patient1DischargeEvent = patient1DischargePayload.CreatePatientEvent(facilityId, dischargeCorrelationId1);
        //events.Add(patient1DischargeEvent);

        // Within date range, admit event, patient 2
        var admitCorrelationId2 = Guid.NewGuid().ToString();
        var patient2AdmitPayload = new FHIRListAdmitPayload(patientId2, DateTime.UtcNow.AddDays(-2));
        var patient2AdmitEvent = patient2AdmitPayload.CreatePatientEvent(facilityId, admitCorrelationId2);
        events.Add(patient2AdmitEvent);

        // Outside date range, admit event
        var outsidePatientId = Guid.NewGuid().ToString();
        var outsideCorrelationId = Guid.NewGuid().ToString();
        var outsideAdmitPayload = new FHIRListAdmitPayload(outsidePatientId, DateTime.UtcNow.AddDays(-10));
        var outsideAdmitEvent = outsideAdmitPayload.CreatePatientEvent(facilityId, outsideCorrelationId);
        events.Add(outsideAdmitEvent);

        // Different facility, within date range
        var otherFacilityPatientId = Guid.NewGuid().ToString();
        var otherFacilityCorrelationId = Guid.NewGuid().ToString();
        var otherFacilityPayload = new FHIRListAdmitPayload(otherFacilityPatientId, DateTime.UtcNow.AddDays(-3));
        var otherFacilityEvent = otherFacilityPayload.CreatePatientEvent("OtherFacility", otherFacilityCorrelationId);
        events.Add(otherFacilityEvent);

        await db.PatientEvents.AddRangeAsync(events);
        await db.SaveChangesAsync();

        // Act
        var results =
            await queries.GetAdmittedPatientEventModelsByDateRange(facilityId, startDate, endDate,
                CancellationToken.None);

        // Assert
        Assert.NotNull(results);
        // Should return 2 events (latest events for each patient within the date range)
        Assert.Equal(2, results?.Count() ?? 0);

        // Verify both admitted patients are in the results
        Assert.Contains(results, e => e.SourcePatientId == patientId1);
        Assert.Contains(results, e => e.SourcePatientId == patientId2);

        foreach (var result in results)
        {
            // Extract the date from the payload
            var eventDate = GetDateFromPayload(result.Payload);

            Assert.True(eventDate >= startDate && eventDate <= endDate,
                $"Event date {eventDate} for event {result.Id} should be between {startDate} and {endDate}");
        }


    }

    private DateTime GetDateFromPayload(IPayload payload)
    {
        return payload switch
        {
            null => throw new ArgumentException("Payload is null or empty"),
            FHIRListAdmitPayload admitPayload => admitPayload.AdmitDate,
            FHIRListDischargePayload dischargePayload => dischargePayload.DischargeDate,
            _ => throw new ArgumentException("Could not find date information in payload")
        };
    }


    [Fact]
    public async Task GetAdmittedPatientEventModelsByDateRange_WithInvalidParameters_ThrowsArgumentException()
    {
        // Arrange
        var db = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();

        var facilityId = "TestFacility" + Guid.NewGuid().ToString();
        var startDate = DateTime.UtcNow.AddDays(-5);
        var endDate = DateTime.UtcNow;

        // Act & Assert - Invalid facility ID
        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetAdmittedPatientEventModelsByDateRange(null, startDate, endDate, CancellationToken.None));

        // Act & Assert - Default start date
        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetAdmittedPatientEventModelsByDateRange(facilityId, default, endDate, CancellationToken.None));

        // Act & Assert - Default end date
        await Assert.ThrowsAsync<ArgumentException>(() =>
            queries.GetAdmittedPatientEventModelsByDateRange(facilityId, startDate, default, CancellationToken.None));
    }

    [Fact]
    public async Task RebuildPatientEncounterTable_PopulatesEncountersFromEvents()
    {
        // Arrange
        var dbContext = _fixture.DbContext;
        var queries = _fixture.ServiceProvider.GetRequiredService<IPatientEventQueries>();
        var patientEncounterQueries = _fixture.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();

        // Clear existing data from patient encounters table and related tables
        dbContext.PatientIdentifiers.RemoveRange(dbContext.PatientIdentifiers);
        dbContext.PatientVisitIdentifiers.RemoveRange(dbContext.PatientVisitIdentifiers);
        dbContext.PatientEncounters.RemoveRange(dbContext.PatientEncounters);
        await dbContext.SaveChangesAsync();

        // Seed the database with patient events
        await SeedData.SeedPatientEvents(dbContext);

        // Get initial event count for comparison
        var eventCount = await dbContext.PatientEvents.CountAsync();
        Assert.True(eventCount > 0, "Database should have patient events after seeding");

        var initialEncounterCount = await dbContext.PatientEncounters.CountAsync();
        Assert.Equal(0, initialEncounterCount);

        // Act
        await patientEncounterQueries.RebuildPatientEncounterTable();

        // Assert
        var resultEncounterCount = await dbContext.PatientEncounters.CountAsync();
        Assert.True(resultEncounterCount > 0, "PatientEncounters table should be populated after rebuild");

        // Verify all facilities have patient encounters
        var facilityIds = new[] { "Facility1", "Facility2", "Facility3", "Facility4", "Facility5" };
        foreach (var facilityId in facilityIds)
        {
            var facilityEncounterCount = await dbContext.PatientEncounters
                .CountAsync(pe => pe.FacilityId == facilityId);
            Assert.True(facilityEncounterCount > 0, $"Facility {facilityId} should have encounters");
        }

        // Verify encounter data and relationships
        // Check that encounters have patient identifiers
        var encounterWithIdentifiers = await dbContext.PatientEncounters
            .Include(pe => pe.PatientIdentifiers)
            .Include(pe => pe.PatientVisitIdentifiers)
            .FirstOrDefaultAsync();

        Assert.NotNull(encounterWithIdentifiers);
        Assert.NotEmpty(encounterWithIdentifiers.PatientIdentifiers);

        // Check a sample event and its corresponding encounter by correlationId
        var sampleEvent = await dbContext.PatientEvents.FirstOrDefaultAsync();
        Assert.NotNull(sampleEvent);

        var correspondingEncounter = await dbContext.PatientEncounters
            .Include(pe => pe.PatientIdentifiers)
            .FirstOrDefaultAsync(pe => pe.CorrelationId == sampleEvent.CorrelationId);

        Assert.NotNull(correspondingEncounter);
        Assert.Equal(sampleEvent.FacilityId, correspondingEncounter.FacilityId);

        // Verify that the patient ID from the event matches one of the identifiers
        // in the PatientIdentifiers collection
        var patientIdFromEvent = sampleEvent.SourcePatientId;
        Assert.Contains(correspondingEncounter.PatientIdentifiers,
            pi => pi.Identifier == patientIdFromEvent);
    }

    #endregion




}