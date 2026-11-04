using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Adt;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

[Collection("IntegrationTests")]
public class AdtAcquiredWorkflowTests
{
    private const string PatientId = "fhir-1";
    private const string VisitId = "visit-1";

    private readonly CensusIntegrationTestFixture _fixture;
    private readonly string _facilityId = "TestFacility" + Guid.NewGuid();
    private DateTime _nextMessageDate = new(2026, 2, 1, 8, 0, 0);

    public AdtAcquiredWorkflowTests(CensusIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private AdtEventEntry Entry(string type, string? patientId = PatientId, string? visitId = VisitId, Action<AdtEventEntry>? configure = null)
    {
        _nextMessageDate = _nextMessageDate.AddHours(1);

        var entry = new AdtEventEntry
        {
            FhirPatientId = patientId ?? string.Empty,
            MedicalRecordNumber = "mrn-1",
            EncounterId = visitId,
            MessageType = type,
            MessageDateTime = _nextMessageDate,
            PatientClass = "I",
            Location = "4E^401^A",
            PatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-1", IdentifierType = "MR" }],
        };

        configure?.Invoke(entry);
        return entry;
    }

    /// <summary>
    /// Processes in a new scope, as the listener does for each Kafka message. Returns "Admit" and "Discharge" messages in order.
    /// </summary>
    private async Task<List<string>> Process(params AdtEventEntry[] entries)
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = new AdtEventService(
            NullLogger<AdtEventService>.Instance,
            new NullCensusServiceMetrics(),
            scope.ServiceProvider.GetRequiredService<IPatientEventManager>(),
            scope.ServiceProvider.GetRequiredService<IPatientEventQueries>(),
            scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>(),
            scope.ServiceProvider.GetRequiredService<IPatientEncounterManager>());

        var responses = await service.ProcessEvents(_facilityId, new ADTAcquired { Events = [.. entries] }, CancellationToken.None);

        return responses.Cast<PatientEventResponse>().Select(r =>
        {
            Assert.Equal(_facilityId, r.FacilityId);
            return r.PatientEvent!.EventType;
        }).ToList();
    }

    private async Task<List<PatientEncounter>> Encounters()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CensusContext>().PatientEncounters
            .AsNoTracking()
            .Include(e => e.PatientIdentifiers)
            .Include(e => e.PatientVisitIdentifiers)
            .Where(e => e.FacilityId == _facilityId)
            .OrderBy(e => e.CreateDate)
            .ToListAsync();
    }

    private async Task<List<PatientEvent>> Events()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CensusContext>().PatientEvents
            .AsNoTracking()
            .Where(e => e.FacilityId == _facilityId)
            .ToListAsync();
    }

    private static readonly string[] Admit = [nameof(PatientEvents.Admit)];
    private static readonly string[] Discharge = [nameof(PatientEvents.Discharge)];
    private static readonly string[] AdmitAndDischarge = [nameof(PatientEvents.Admit), nameof(PatientEvents.Discharge)];

    [Fact]
    public async Task A01_EmitsAdmit_AndCreatesTheEncounterAndIdentifiers()
    {
        var admitDate = new DateTime(2026, 2, 1, 7, 0, 0);

        var messages = await Process(Entry("A01", configure: e => e.AdmitDateTime = admitDate));

        Assert.Equal(Admit, messages);

        var encounter = Assert.Single(await Encounters());
        Assert.Equal(admitDate, encounter.AdmitDate);
        Assert.Null(encounter.DischargeDate);
        Assert.Equal("mrn-1", encounter.MedicalRecordNumber);
        Assert.Equal("I", encounter.EncounterClass);
        Assert.Equal("4E^401^A", encounter.Location);
        Assert.Equal("in-progress", encounter.EncounterStatus);
        Assert.Contains(encounter.PatientIdentifiers, i => i.Identifier == PatientId && i.SourceType == nameof(SourceType.FHIR));
        Assert.Contains(encounter.PatientIdentifiers, i => i.Identifier == "mrn-1" && i.SourceType == nameof(SourceType.ADT));
        Assert.Contains(encounter.PatientVisitIdentifiers, i => i.Identifier == VisitId && i.SourceType == nameof(SourceType.ADT));

        var patientEvent = Assert.Single(await Events());
        Assert.Equal(EventType.A01, patientEvent.EventType);
        Assert.Equal(SourceType.ADT, patientEvent.SourceType);
        Assert.Equal(PatientId, patientEvent.SourcePatientId);
        Assert.Equal(VisitId, patientEvent.SourceVisitId);
        Assert.Equal(encounter.CorrelationId, patientEvent.CorrelationId);
        Assert.IsType<AdtA01Payload>(patientEvent.Payload);
    }

    [Fact]
    public async Task A03_AfterA01_EmitsDischarge_AndClosesTheEncounter()
    {
        var dischargeDate = new DateTime(2026, 2, 3);

        var messages = await Process(Entry("A01"), Entry("A03", configure: e => e.DischargeDateTime = dischargeDate));

        Assert.Equal(AdmitAndDischarge, messages);
        var encounter = Assert.Single(await Encounters());
        Assert.Equal(dischargeDate, encounter.DischargeDate);
        Assert.Equal("finished", encounter.EncounterStatus);
    }

    [Fact]
    public async Task A03_AsFirstEvent_EmitsAdmitAndDischarge()
    {
        var messages = await Process(Entry("A03"));

        Assert.Equal(AdmitAndDischarge, messages);
        Assert.NotNull(Assert.Single(await Encounters()).DischargeDate);
    }

    [Theory]
    [InlineData("A02")]
    [InlineData("A06")]
    [InlineData("A07")]
    [InlineData("A12")]
    [InlineData("A13")]
    public async Task AnotherEvent_AsFirstEvent_EmitsAdmit(string type)
    {
        var messages = await Process(Entry(type));

        Assert.Equal(Admit, messages);
        Assert.Null(Assert.Single(await Encounters()).DischargeDate);
    }

    [Fact]
    public async Task A01_AfterAnEarlierEvent_EmitsNoSecondAdmit_AndFillsInTheAdmitDate()
    {
        var admitDate = new DateTime(2026, 1, 15);

        Assert.Equal(Admit, await Process(Entry("A02")));
        Assert.Empty(await Process(Entry("A01", configure: e => e.AdmitDateTime = admitDate)));

        Assert.Equal(admitDate, Assert.Single(await Encounters()).AdmitDate);
    }

    [Fact]
    public async Task A01_AfterTheStayEnded_IsANewEncounter()
    {
        await Process(Entry("A01"), Entry("A03"));

        var messages = await Process(Entry("A01", visitId: "visit-2"));

        Assert.Equal(Admit, messages);
        var encounters = await Encounters();
        Assert.Equal(2, encounters.Count);
        Assert.NotEqual(encounters[0].CorrelationId, encounters[1].CorrelationId);
    }

    [Fact]
    public async Task EventsWithoutAnEncounterId_FollowTheOpenStay()
    {
        await Process(Entry("A01", visitId: null));

        Assert.Empty(await Process(Entry("A02", visitId: null, configure: e => e.Location = "5W^1")));
        Assert.Equal("5W^1", Assert.Single(await Encounters()).Location);

        Assert.Equal(Discharge, await Process(Entry("A03", visitId: null)));
        Assert.Equal(Admit, await Process(Entry("A01", visitId: null)));
        Assert.Equal(2, (await Encounters()).Count);
    }

    [Fact]
    public async Task EncounterIdFirstSeenLater_IsAttachedToTheOpenStay()
    {
        await Process(Entry("A01", visitId: null));

        await Process(Entry("A02", visitId: VisitId));

        Assert.Contains((await Encounters()).Single().PatientVisitIdentifiers, v => v.Identifier == VisitId);
    }

    [Fact]
    public async Task EventsForTwoEncountersOfOnePatient_AreKeptApart()
    {
        await Process(Entry("A01", visitId: "visit-a"), Entry("A01", visitId: "visit-b"));

        Assert.Empty(await Process(Entry("A02", visitId: "visit-a", configure: e => e.Location = "MOVED")));

        var encounters = await Encounters();
        Assert.Equal(2, encounters.Count);
        Assert.Equal("MOVED", encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "visit-a")).Location);
        Assert.Equal("4E^401^A", encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "visit-b")).Location);
    }

    [Fact]
    public async Task A02_UpdatesLocation_AndA12_RestoresIt_WithoutMessages()
    {
        await Process(Entry("A01"));

        Assert.Empty(await Process(Entry("A02", configure: e => e.Location = "5W^1")));
        Assert.Equal("5W^1", Assert.Single(await Encounters()).Location);

        Assert.Empty(await Process(Entry("A12")));
        Assert.Equal("4E^401^A", Assert.Single(await Encounters()).Location);
    }

    [Fact]
    public async Task A06AndA07_UpdateTheClass_WithoutMessages()
    {
        await Process(Entry("A01", configure: e => e.PatientClass = "O"));

        Assert.Empty(await Process(Entry("A06", configure: e => e.PatientClass = "I")));
        Assert.Equal("I", Assert.Single(await Encounters()).EncounterClass);

        Assert.Empty(await Process(Entry("A07", configure: e => e.PatientClass = "O")));
        Assert.Equal("O", Assert.Single(await Encounters()).EncounterClass);
    }

    [Fact]
    public async Task A11_CancelsTheEncounter_WithoutMessages_AndALaterA01StartsAnother()
    {
        await Process(Entry("A01"));

        Assert.Empty(await Process(Entry("A11")));

        var cancelled = Assert.Single(await Encounters());
        Assert.Equal("cancelled", cancelled.EncounterStatus);
        Assert.Equal(cancelled.AdmitDate, cancelled.DischargeDate);

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var admitted = await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().GetAdmittedPatientsByFacility(_facilityId);
            Assert.Empty(admitted);
        }

        Assert.Equal(Admit, await Process(Entry("A01")));
        Assert.Equal(2, (await Encounters()).Count);
    }

    [Fact]
    public async Task A11_AsFirstEvent_IsRecordedButOpensNothing()
    {
        Assert.Empty(await Process(Entry("A11")));

        Assert.Empty(await Encounters());
        Assert.Equal(EventType.A11, Assert.Single(await Events()).EventType);
    }

    [Fact]
    public async Task A13_ReopensTheEncounter_WithoutMessages_AndALaterA03DischargesAgain()
    {
        await Process(Entry("A01"), Entry("A03"));

        Assert.Empty(await Process(Entry("A13")));
        Assert.Null(Assert.Single(await Encounters()).DischargeDate);

        Assert.Equal(Discharge, await Process(Entry("A03")));
    }

    [Fact]
    public async Task A13_WithoutAnEncounterId_ReopensTheLatestStay()
    {
        await Process(Entry("A01", visitId: null), Entry("A03", visitId: null));

        Assert.Empty(await Process(Entry("A13", visitId: null)));

        Assert.Null(Assert.Single(await Encounters()).DischargeDate);
    }

    [Fact]
    public async Task A03_ForAStayAlreadyDischarged_EmitsNoSecondDischarge()
    {
        await Process(Entry("A01"), Entry("A03"));

        Assert.Empty(await Process(Entry("A03")));
    }

    [Fact]
    public async Task A18_MovesEncountersToTheSurvivingPatient_WithoutMessages()
    {
        await Process(Entry("A01"));

        var messages = await Process(Entry("A18", patientId: "fhir-2", visitId: null, configure: e =>
        {
            e.MedicalRecordNumber = "mrn-2";
            e.PatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-2", IdentifierType = "MR" }];
            e.Merge = new AdtMergeInformation { PriorPatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-1" }] };
        }));

        Assert.Empty(messages);

        var encounter = Assert.Single(await Encounters());
        Assert.Equal("mrn-2", encounter.MedicalRecordNumber);
        Assert.Equal("fhir-2", encounter.GetIdentifierByType(SourceType.FHIR).Identifier);
        Assert.Contains(encounter.PatientIdentifiers, i => i.Identifier == "mrn-1");
        Assert.Contains(encounter.PatientIdentifiers, i => i.Identifier == "mrn-2");

        var merge = Assert.Single(await Events(), e => e.EventType == EventType.A18);
        Assert.Equal(encounter.CorrelationId, merge.CorrelationId);
        Assert.Equal("mrn-1", ((AdtA18Payload)merge.Payload).Merge!.PriorPatientIdentifierList[0].Id);

        // The stay now belongs to the surviving patient
        Assert.Equal(Discharge, await Process(Entry("A03", patientId: "fhir-2")));
    }

    [Fact]
    public async Task A18_MatchingNothing_IsRecordedButOpensNothing()
    {
        var messages = await Process(Entry("A18", configure: e =>
            e.Merge = new AdtMergeInformation { PriorPatientIdentifierList = [new AdtPatientIdentifier { Id = "unknown" }] }));

        Assert.Empty(messages);
        Assert.Empty(await Encounters());
        Assert.Single(await Events());
    }

    [Theory]
    [InlineData("A04")]
    [InlineData("A08")]
    [InlineData("A103")]
    [InlineData("")]
    public async Task OtherMessageTypes_AreIgnored(string type)
    {
        Assert.Empty(await Process(Entry(type)));

        Assert.Empty(await Encounters());
        Assert.Empty(await Events());
    }

    [Fact]
    public async Task RepeatedMessage_IsIgnored_SoReadingAFileTwiceChangesNothing()
    {
        var admit = Entry("A01");
        var discharge = Entry("A03");

        Assert.Equal(AdmitAndDischarge, await Process(admit, discharge));
        Assert.Empty(await Process(admit, discharge));

        Assert.Single(await Encounters());
        Assert.Equal(2, (await Events()).Count);
    }

    [Fact]
    public async Task MessageDateTime_IsTheEventDate_AndTheEventIsStillRecordedWithoutOne()
    {
        var admit = Entry("A01");
        await Process(admit, Entry("A02", configure: e => e.MessageDateTime = null));

        var events = await Events();
        Assert.Equal(admit.MessageDateTime, events.Single(e => e.EventType == EventType.A01).EventDate);
        Assert.NotEqual(default, events.Single(e => e.EventType == EventType.A02).EventDate);
    }

    [Fact]
    public async Task EventsAreAppliedInOrder_AcrossPatients()
    {
        var messages = await Process(
            Entry("A01", patientId: "fhir-a", visitId: "a"),
            Entry("A01", patientId: "fhir-b", visitId: "b"),
            Entry("A03", patientId: "fhir-a", visitId: "a"));

        Assert.Equal([nameof(PatientEvents.Admit), nameof(PatientEvents.Admit), nameof(PatientEvents.Discharge)], messages);
    }

    [Fact]
    public async Task Rebuild_ReproducesTheEncounterFromItsEvents()
    {
        var dischargeDate = new DateTime(2026, 2, 5);
        await Process(
            Entry("A02", configure: e => e.Location = "5W^1"),
            Entry("A01", configure: e => e.AdmitDateTime = new DateTime(2026, 1, 15)),
            Entry("A03", configure: e => e.DischargeDateTime = dischargeDate));
        var before = Assert.Single(await Encounters());

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().RebuildPatientEncounterTable(_facilityId);
        }

        var after = Assert.Single(await Encounters());
        Assert.Equal(before.CorrelationId, after.CorrelationId);
        Assert.Equal(before.AdmitDate, after.AdmitDate);
        Assert.Equal(dischargeDate, after.DischargeDate);
        Assert.Equal(before.Location, after.Location);
        Assert.Equal(before.EncounterStatus, after.EncounterStatus);
        Assert.Equal(before.PatientIdentifiers.Count, after.PatientIdentifiers.Count);
        Assert.Equal(before.PatientVisitIdentifiers.Count, after.PatientVisitIdentifiers.Count);
    }

    [Fact]
    public async Task PatientEventsApi_ReturnsTheFullPayload_AndEncounterApi_ReturnsTheLocation()
    {
        await Process(Entry("A01"));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        using var scope = _fixture.ServiceProvider.CreateScope();

        var events = await scope.ServiceProvider.GetRequiredService<IPatientEventQueries>().GetPagedPatientEvents(_facilityId);
        var eventJson = JsonSerializer.SerializeToDocument(Assert.Single(events.Records), options).RootElement;
        Assert.Equal("A01", eventJson.GetProperty("eventType").GetString());
        Assert.NotEqual(default, eventJson.GetProperty("eventDate").GetDateTime());
        Assert.Equal("A01", eventJson.GetProperty("payload").GetProperty("PayloadType").GetString());
        Assert.Equal("4E^401^A", eventJson.GetProperty("payload").GetProperty("Location").GetString());

        var encounters = await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().GetPagedCurrentPatientEncounters(_facilityId);
        Assert.Equal("4E^401^A", Assert.Single(encounters.Records).Location);
    }

    [Fact]
    public async Task AdmittedPatientsApi_ListsTheFhirPatientId_NotAnAdtIdentifier()
    {
        await Process(Entry("A01"));

        using var scope = _fixture.ServiceProvider.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>();
        var controller = new LantanaGroup.Link.Census.Controllers.CensusController(
            NullLogger<LantanaGroup.Link.Census.Controllers.CensusController>.Instance,
            queries,
            scope.ServiceProvider.GetRequiredService<IPatientEventQueries>());

        var result = await controller.GetAdmittedPatients(_facilityId, new DateTime(2020, 1, 1), new DateTime(2099, 1, 1));

        var list = Assert.IsType<Hl7.Fhir.Model.List>(Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value);
        Assert.Equal($"Patient/{PatientId}", Assert.Single(list.Entry).Item.Reference);
        Assert.Equal([PatientId], await queries.GetCurrentlyAdmittedPatientsForFacility(_facilityId));
    }

    [Theory]
    [InlineData("A01", typeof(AdtA01Payload))]
    [InlineData("A02", typeof(AdtA02Payload))]
    [InlineData("A03", typeof(AdtA03Payload))]
    [InlineData("A06", typeof(AdtA06Payload))]
    [InlineData("A07", typeof(AdtA07Payload))]
    [InlineData("A11", typeof(AdtA11Payload))]
    [InlineData("A12", typeof(AdtA12Payload))]
    [InlineData("A13", typeof(AdtA13Payload))]
    [InlineData("A18", typeof(AdtA18Payload))]
    public void EachMessageType_RoundTripsThroughItsOwnPayloadType(string type, Type expected)
    {
        var entry = Entry(type);
        var payload = AdtPayloadFactory.Create(entry);
        Assert.IsType(expected, payload);
        Assert.Equal(type, payload!.PayloadType);

        var json = JsonSerializer.Serialize<IPayload>(payload, CensusContext.JsonSerializerOptionsProvider.Options);
        var read = JsonSerializer.Deserialize<IPayload>(json, CensusContext.JsonSerializerOptionsProvider.Options);

        Assert.IsType(expected, read);
        var adt = (AdtPayload)read!;
        Assert.Equal(PatientId, adt.PatientId);
        Assert.Equal(VisitId, adt.EncounterId);
        Assert.Equal(entry.MessageDateTime, adt.MessageDateTime);
        Assert.Equal("4E^401^A", adt.Location);
        Assert.Equal("mrn-1", adt.PatientIdentifierList.Single().Id);
    }
}
