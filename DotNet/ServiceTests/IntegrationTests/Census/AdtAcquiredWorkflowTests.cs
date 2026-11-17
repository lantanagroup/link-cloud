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
        Assert.Null(encounter.EncounterClass);
        Assert.Null(encounter.EncounterStatus);
        Assert.Contains(encounter.PatientIdentifiers, i => i.Identifier == PatientId && i.SourceType == nameof(SourceType.ADT_FHIR));
        Assert.DoesNotContain(encounter.PatientIdentifiers, i => i.Identifier == "mrn-1");
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
    public async Task MrnsAndTheFhirIdAreLeftOutOfAdtIdentifiers_WhileOtherIdentifiersAreKept()
    {
        await Process(Entry("A01", configure: e =>
        {
            // The MRN by type and by value, the FHIR id alone and as a reference, and an identifier that is none of these
            e.PatientIdentifierList =
            [
                new AdtPatientIdentifier { Id = "other-mrn", IdentifierType = "mr" },
                new AdtPatientIdentifier { Id = "mrn-1", IdentifierType = "PI" },
                new AdtPatientIdentifier { Id = PatientId, IdentifierType = "FHIR" },
                new AdtPatientIdentifier { Id = $"Patient/{PatientId}", IdentifierType = "FHIR" },
                new AdtPatientIdentifier { Id = "ssn-1", IdentifierType = "SS" },
            ];
        }));

        async Task AssertIdentifiers()
        {
            var encounter = Assert.Single(await Encounters());
            Assert.Equal("mrn-1", encounter.MedicalRecordNumber);
            Assert.Equal(
                [(PatientId, nameof(SourceType.ADT_FHIR)), ("ssn-1", nameof(SourceType.ADT))],
                encounter.PatientIdentifiers.Select(i => (i.Identifier, i.SourceType)).OrderBy(i => i.SourceType == nameof(SourceType.ADT)));

            // The stored message keeps everything it was sent
            var patientEvent = Assert.Single(await Events());
            Assert.Equal(5, ((AdtA01Payload)patientEvent.Payload).PatientIdentifierList.Count);
        }

        await AssertIdentifiers();

        // A rebuild replays the same payloads, so it must reach the same result
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().RebuildPatientEncounterTable(_facilityId);
        }

        await AssertIdentifiers();
    }

    [Fact]
    public async Task A03_AfterA01_EmitsDischarge_AndClosesTheEncounter()
    {
        var dischargeDate = new DateTime(2026, 2, 3);

        var messages = await Process(Entry("A01"), Entry("A03", configure: e => e.DischargeDateTime = dischargeDate));

        Assert.Equal(AdmitAndDischarge, messages);
        var encounter = Assert.Single(await Encounters());
        Assert.Equal(dischargeDate, encounter.DischargeDate);
    }

    [Fact]
    public async Task A03_AsFirstEvent_EmitsAdmitAndDischarge()
    {
        var messages = await Process(Entry("A03"));

        Assert.Equal(AdmitAndDischarge, messages);
        Assert.NotNull(Assert.Single(await Encounters()).DischargeDate);
    }

    [Theory]
    [InlineData("A06")]
    [InlineData("A07")]
    [InlineData("A13")]
    [InlineData("A04")]
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

        Assert.Equal(Admit, await Process(Entry("A06")));
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

        Assert.Empty(await Process(Entry("A07", visitId: null)));
        Assert.Single(await Encounters());

        Assert.Equal(Discharge, await Process(Entry("A03", visitId: null)));
        Assert.Equal(Admit, await Process(Entry("A01", visitId: null)));
        Assert.Equal(2, (await Encounters()).Count);
    }

    [Fact]
    public async Task EncounterIdFirstSeenLater_IsAttachedToTheOpenStay()
    {
        await Process(Entry("A01", visitId: null));

        await Process(Entry("A06", visitId: VisitId));

        Assert.Contains((await Encounters()).Single().PatientVisitIdentifiers, v => v.Identifier == VisitId);
    }

    [Fact]
    public async Task EventsForTwoEncountersOfOnePatient_AreKeptApart()
    {
        await Process(Entry("A01", visitId: "visit-a"), Entry("A01", visitId: "visit-b"));

        Assert.Equal(Discharge, await Process(Entry("A03", visitId: "visit-a")));

        var encounters = await Encounters();
        Assert.Equal(2, encounters.Count);
        Assert.NotNull(encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "visit-a")).DischargeDate);
        Assert.Null(encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "visit-b")).DischargeDate);
    }

    [Theory]
    [InlineData("A02")]
    [InlineData("A12")]
    public async Task TransferMessages_ChangeNothing_OnAnOpenStay(string type)
    {
        await Process(Entry("A01"));
        var before = Assert.Single(await Encounters());

        Assert.Empty(await Process(Entry(type, configure: e => e.PatientClass = "O")));

        var after = Assert.Single(await Encounters());
        Assert.Equal(before.ModifyDate, after.ModifyDate);
        Assert.Single(await Events());
    }

    [Theory]
    [InlineData("A02")]
    [InlineData("A12")]
    public async Task TransferMessage_BeforeAnyTrackedMessage_OpensNothing_UntilOneArrives(string type)
    {
        Assert.Empty(await Process(Entry(type)));
        Assert.Empty(await Encounters());
        Assert.Empty(await Events());

        Assert.Equal(AdmitAndDischarge, await Process(Entry("A03")));
        Assert.Single(await Encounters());
    }

    [Fact]
    public async Task A06AndA07_EmitNoMessages_AndLeaveTheClassAndStatusUnset()
    {
        await Process(Entry("A01", configure: e => e.PatientClass = "O"));

        Assert.Empty(await Process(Entry("A06", configure: e => e.PatientClass = "I")));
        Assert.Empty(await Process(Entry("A07", configure: e => e.PatientClass = "O")));

        var encounter = Assert.Single(await Encounters());
        Assert.Null(encounter.EncounterClass);
        Assert.Null(encounter.EncounterStatus);
    }

    [Theory]
    [InlineData("A11")]
    [InlineData("A23")]
    public async Task Cancellation_ClosesTheEncounter_WithoutMessages_AndALaterA01StartsAnother(string type)
    {
        await Process(Entry("A01"));

        Assert.Empty(await Process(Entry(type)));

        var cancelled = Assert.Single(await Encounters());
        Assert.Null(cancelled.EncounterStatus);
        Assert.Equal(cancelled.AdmitDate, cancelled.DischargeDate);

        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var admitted = await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().GetAdmittedPatientsByFacility(_facilityId);
            Assert.Empty(admitted);
        }

        // The same encounter id, as when the patient is admitted again after the cancellation
        Assert.Equal(Admit, await Process(Entry("A01")));

        async Task AssertStays()
        {
            var encounters = await Encounters();
            Assert.Equal(2, encounters.Count);
            Assert.NotEqual(encounters[0].CorrelationId, encounters[1].CorrelationId);

            var closed = encounters.Single(e => e.DischargeDate is not null);
            Assert.Equal(closed.AdmitDate, closed.DischargeDate);
            Assert.Single(encounters, e => e.DischargeDate is null);
        }

        await AssertStays();

        // A rebuild replays the same payloads, so it must reach the same result
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().RebuildPatientEncounterTable(_facilityId);
        }

        await AssertStays();

        // Later messages belong to the new stay
        var discharge = new DateTime(2026, 3, 1);
        Assert.Equal(Discharge, await Process(Entry("A03", configure: e => e.DischargeDateTime = discharge)));
        Assert.Contains(await Encounters(), e => e.DischargeDate == discharge);
    }

    [Theory]
    [InlineData("A11")]
    [InlineData("A23")]
    public async Task A01_AfterACancellation_InTheSameFile_StartsAnotherStay(string type)
    {
        var messages = await Process(Entry("A01"), Entry(type), Entry("A01"));

        Assert.Equal([nameof(PatientEvents.Admit), nameof(PatientEvents.Admit)], messages);
        Assert.Equal(2, (await Encounters()).Count);
    }

    [Theory]
    [InlineData("A11")]
    [InlineData("A23")]
    [InlineData("A08")]
    public async Task UpdateOrCancellation_AsFirstEvent_IsRecordedButOpensNothing(string type)
    {
        Assert.Empty(await Process(Entry(type)));

        Assert.Empty(await Encounters());
        Assert.Equal(Enum.Parse<EventType>(type), Assert.Single(await Events()).EventType);
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
        Assert.Equal("fhir-2", encounter.GetIdentifierByType(SourceType.ADT_FHIR).Identifier);
        Assert.DoesNotContain(encounter.PatientIdentifiers, i => i.Identifier == "mrn-1");
        Assert.DoesNotContain(encounter.PatientIdentifiers, i => i.Identifier == "mrn-2");

        var merge = Assert.Single(await Events(), e => e.EventType == EventType.A18);
        Assert.Equal(encounter.CorrelationId, merge.CorrelationId);
        Assert.Equal("mrn-1", ((AdtA18Payload)merge.Payload).Merge!.PriorPatientIdentifierList[0].Id);

        // The stay now belongs to the surviving patient
        Assert.Equal(Discharge, await Process(Entry("A03", patientId: "fhir-2")));
    }

    [Fact]
    public async Task A18_OnlyChangesPatientIdentifiers_OnEveryStayOfThePatient()
    {
        var discharge = new DateTime(2026, 1, 20);
        await Process(
            Entry("A01", visitId: "old-stay", configure: e => e.AdmitDateTime = new DateTime(2026, 1, 10)),
            Entry("A03", visitId: "old-stay", configure: e => e.DischargeDateTime = discharge),
            Entry("A01", visitId: "open-stay", configure: e => e.AdmitDateTime = new DateTime(2026, 2, 1)));

        // The merge message's PV1 describes one visit of the surviving patient, not every stay it matches
        var messages = await Process(Entry("A18", patientId: "fhir-2", visitId: "merge-visit", configure: e =>
        {
            e.MedicalRecordNumber = "mrn-2";
            e.PatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-2", IdentifierType = "MR" }];
            e.Merge = new AdtMergeInformation { PriorPatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-1" }] };
        }));

        Assert.Empty(messages);

        async Task AssertStays()
        {
            var encounters = await Encounters();
            Assert.Equal(2, encounters.Count);

            // Each stay moved to the surviving patient, whose MRN replaced the prior one
            Assert.All(encounters, e =>
            {
                Assert.Equal("mrn-2", e.MedicalRecordNumber);
                Assert.Equal("fhir-2", e.GetIdentifierByType(SourceType.ADT_FHIR).Identifier);
                Assert.DoesNotContain(e.PatientIdentifiers, i => i.Identifier == "mrn-1");
                Assert.DoesNotContain(e.PatientIdentifiers, i => i.Identifier == "mrn-2");
                Assert.DoesNotContain(e.PatientVisitIdentifiers, v => v.Identifier == "merge-visit");
            });

            var old = encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "old-stay"));
            Assert.Equal(discharge, old.DischargeDate);

            var open = encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "open-stay"));
            Assert.Null(open.DischargeDate);
        }

        await AssertStays();

        // A rebuild replays the same payloads, so it must reach the same result
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().RebuildPatientEncounterTable(_facilityId);
        }

        await AssertStays();
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
    [InlineData("A40")]
    [InlineData("A34")]
    [InlineData("A36")]
    [InlineData("A39")]
    [InlineData("A30")]
    public async Task PatientMerges_AreHandledAsA18(string type)
    {
        await Process(Entry("A01"));

        Assert.Empty(await Process(Entry(type, patientId: "fhir-2", visitId: null, configure: e =>
        {
            e.MedicalRecordNumber = "mrn-2";
            e.PatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-2", IdentifierType = "MR" }];
            e.Merge = new AdtMergeInformation { PriorPatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-1" }] };
        })));

        var encounter = Assert.Single(await Encounters());
        Assert.Equal("mrn-2", encounter.MedicalRecordNumber);
        Assert.Equal("fhir-2", encounter.GetIdentifierByType(SourceType.ADT_FHIR).Identifier);

        var merge = Assert.Single(await Events(), e => e.EventType == Enum.Parse<EventType>(type));
        Assert.Equal(encounter.CorrelationId, merge.CorrelationId);
        Assert.IsAssignableFrom<AdtPatientMergePayload>(merge.Payload);

        Assert.Equal(Discharge, await Process(Entry("A03", patientId: "fhir-2")));
    }

    [Fact]
    public async Task A04_SetsTheAdmitDate_AndItsA03DischargesTheSameStay()
    {
        var admitDate = new DateTime(2026, 2, 1, 7, 30, 0);

        Assert.Equal(Admit, await Process(Entry("A04", configure: e => { e.PatientClass = "E"; e.AdmitDateTime = admitDate; })));
        Assert.Equal(Discharge, await Process(Entry("A03", configure: e => e.PatientClass = "E")));

        var encounter = Assert.Single(await Encounters());
        Assert.Equal(admitDate, encounter.AdmitDate);
        Assert.NotNull(encounter.DischargeDate);
    }

    [Fact]
    public async Task A08_CorrectsTheAdmitDateAndIdentifiers_WithoutMessages()
    {
        await Process(Entry("A01", configure: e => e.AdmitDateTime = new DateTime(2026, 2, 1, 7, 0, 0)));
        var corrected = new DateTime(2026, 1, 31, 22, 0, 0);

        Assert.Empty(await Process(Entry("A08", configure: e =>
        {
            e.AdmitDateTime = corrected;
            e.DischargeDateTime = new DateTime(2026, 2, 3);
            e.PatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-1", IdentifierType = "MR" }, new AdtPatientIdentifier { Id = "ssn-1", IdentifierType = "SS" }];
        })));

        var encounter = Assert.Single(await Encounters());
        Assert.Equal(corrected, encounter.AdmitDate);
        // Only an A03 discharges
        Assert.Null(encounter.DischargeDate);
        Assert.Contains(encounter.PatientIdentifiers, i => i.Identifier == "ssn-1" && i.SourceType == nameof(SourceType.ADT));

        Assert.Equal(Discharge, await Process(Entry("A03")));
    }

    [Fact]
    public async Task A08_CorrectsTheDischargeDate_OfADischargedStay()
    {
        var corrected = new DateTime(2026, 2, 4, 9, 0, 0);
        await Process(Entry("A01"), Entry("A03", configure: e => e.DischargeDateTime = new DateTime(2026, 2, 4, 12, 0, 0)));

        Assert.Empty(await Process(Entry("A08", configure: e => e.DischargeDateTime = corrected)));

        Assert.Equal(corrected, Assert.Single(await Encounters()).DischargeDate);
    }

    private static Action<AdtEventEntry> PriorVisit(string priorVisitId, string? priorMrn = null) => e =>
        e.Merge = new AdtMergeInformation
        {
            PriorVisitNumber = priorVisitId,
            PriorPatientIdentifierList = priorMrn is null ? [] : [new AdtPatientIdentifier { Id = priorMrn, IdentifierType = "MR" }]
        };

    private async Task RebuildEncounters()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPatientEncounterQueries>().RebuildPatientEncounterTable(_facilityId);
    }

    [Theory]
    [InlineData("A50", new[] { "visit-2" })]
    // A merged visit is known by both numbers, so a late message with the prior one still reaches it
    [InlineData("A42", new[] { "visit-1", "visit-2" })]
    public async Task VisitNumberChange_RenamesTheVisit_SoItsNextMessageFollowsTheSameStay(string type, string[] visitIds)
    {
        await Process(Entry("A01", visitId: "visit-1"));

        Assert.Empty(await Process(Entry(type, visitId: "visit-2", configure: PriorVisit("visit-1"))));

        async Task AssertRenamed()
        {
            var encounter = Assert.Single(await Encounters());
            Assert.Equal(visitIds, encounter.PatientVisitIdentifiers.Select(v => v.Identifier).Order());
            Assert.Null(encounter.DischargeDate);
        }

        await AssertRenamed();
        await RebuildEncounters();
        await AssertRenamed();

        // Without the rename, this A03 would open a second stay and emit Admit too
        Assert.Equal(Discharge, await Process(Entry("A03", visitId: "visit-2")));
        Assert.Single(await Encounters());
    }

    [Fact]
    public async Task A42_IntoAVisitAlreadyTracked_ClosesThePriorStay_AndKeepsTheSurvivingOne()
    {
        var survivingAdmit = new DateTime(2026, 2, 1, 6, 0, 0);
        await Process(
            Entry("A01", visitId: "visit-er"),
            Entry("A01", visitId: "visit-in", configure: e => e.AdmitDateTime = survivingAdmit));

        Assert.Empty(await Process(Entry("A42", visitId: "visit-in", configure: PriorVisit("visit-er"))));

        async Task AssertMerged()
        {
            var encounters = await Encounters();
            Assert.Equal(2, encounters.Count);

            var surviving = Assert.Single(encounters, e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "visit-in"));
            Assert.Equal(["visit-er", "visit-in"], surviving.PatientVisitIdentifiers.Select(v => v.Identifier).Order());
            Assert.Equal(survivingAdmit, surviving.AdmitDate);
            Assert.Null(surviving.DischargeDate);

            var prior = Assert.Single(encounters, e => e.Id != surviving.Id);
            Assert.Empty(prior.PatientVisitIdentifiers);
            Assert.Equal(prior.AdmitDate, prior.DischargeDate);
        }

        await AssertMerged();
        Assert.Equal(2, (await Events()).Count(e => e.EventType == EventType.A42));
        await RebuildEncounters();
        await AssertMerged();

        // Messages for either visit number now reach the surviving stay alone
        Assert.Equal(Discharge, await Process(Entry("A03", visitId: "visit-er")));
        Assert.Empty(await Process(Entry("A03", visitId: "visit-in")));
        Assert.Equal(2, (await Encounters()).Count);
    }

    [Fact]
    public async Task A45_MovesOneVisitToAnotherPatient_AndDropsThePriorPatientsIdentifiers()
    {
        await Process(
            Entry("A01", visitId: "visit-1", configure: e => e.PatientIdentifierList =
                [new AdtPatientIdentifier { Id = "mrn-1", IdentifierType = "MR" }, new AdtPatientIdentifier { Id = "ssn-1", IdentifierType = "SS" }]),
            Entry("A01", visitId: "visit-2"));

        var messages = await Process(Entry("A45", patientId: "fhir-2", visitId: "visit-1", configure: e =>
        {
            e.MedicalRecordNumber = "mrn-2";
            e.PatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-2", IdentifierType = "MR" }, new AdtPatientIdentifier { Id = "ssn-2", IdentifierType = "SS" }];
            PriorVisit("visit-1", priorMrn: "mrn-1")(e);
        }));

        Assert.Empty(messages);

        async Task AssertMoved()
        {
            var encounters = await Encounters();
            Assert.Equal(2, encounters.Count);

            var moved = encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "visit-1"));
            Assert.Equal("mrn-2", moved.MedicalRecordNumber);
            Assert.Equal("fhir-2", moved.GetIdentifierByType(SourceType.ADT_FHIR).Identifier);
            Assert.Equal(["ssn-2"], moved.PatientIdentifiers.Where(i => i.SourceType == nameof(SourceType.ADT)).Select(i => i.Identifier));

            // The prior patient's other visit stays theirs
            var kept = encounters.Single(e => e.PatientVisitIdentifiers.Any(v => v.Identifier == "visit-2"));
            Assert.Equal("mrn-1", kept.MedicalRecordNumber);
            Assert.Equal(PatientId, kept.GetIdentifierByType(SourceType.ADT_FHIR).Identifier);
        }

        await AssertMoved();
        await RebuildEncounters();
        await AssertMoved();

        Assert.Equal(Discharge, await Process(Entry("A03", patientId: "fhir-2", visitId: "visit-1")));
    }

    [Fact]
    public async Task A45_WithoutAPriorVisitNumber_MovesTheVisitWithTheMessagesEncounterId()
    {
        await Process(Entry("A01", visitId: "visit-1"));

        await Process(Entry("A45", patientId: "fhir-2", visitId: "visit-1", configure: e =>
            e.Merge = new AdtMergeInformation { PriorPatientIdentifierList = [new AdtPatientIdentifier { Id = "mrn-1" }] }));

        Assert.Equal("fhir-2", Assert.Single(await Encounters()).GetIdentifierByType(SourceType.ADT_FHIR).Identifier);
    }

    [Theory]
    [InlineData("A42")]
    [InlineData("A45")]
    [InlineData("A50")]
    public async Task VisitChange_MatchingNothing_IsRecordedButOpensNothing(string type)
    {
        await Process(Entry("A01", visitId: "visit-1"));

        Assert.Empty(await Process(Entry(type, visitId: "visit-3", configure: PriorVisit("unknown", priorMrn: "mrn-1"))));

        var encounter = Assert.Single(await Encounters());
        Assert.Equal(["visit-1"], encounter.PatientVisitIdentifiers.Select(v => v.Identifier));
        Assert.Equal(2, (await Events()).Count);
    }

    [Theory]
    [InlineData("A05")]
    [InlineData("A31")]
    [InlineData("A103")]
    [InlineData("A02")]
    [InlineData("A12")]
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
        await Process(admit, Entry("A06", configure: e => e.MessageDateTime = null));

        var events = await Events();
        Assert.Equal(admit.MessageDateTime, events.Single(e => e.EventType == EventType.A01).EventDate);
        Assert.NotEqual(default, events.Single(e => e.EventType == EventType.A06).EventDate);
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
            Entry("A07"),
            Entry("A06"),
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
        Assert.Null(after.EncounterClass);
        Assert.Null(after.EncounterStatus);
        Assert.Equal(before.PatientIdentifiers.Count, after.PatientIdentifiers.Count);
        Assert.Equal(before.PatientVisitIdentifiers.Count, after.PatientVisitIdentifiers.Count);
    }

    [Fact]
    public async Task PatientEventsApi_ReturnsTheFullPayload()
    {
        await Process(Entry("A01"));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        using var scope = _fixture.ServiceProvider.CreateScope();

        var events = await scope.ServiceProvider.GetRequiredService<IPatientEventQueries>().GetPagedPatientEvents(_facilityId);
        var eventJson = JsonSerializer.SerializeToDocument(Assert.Single(events.Records), options).RootElement;
        Assert.Equal("A01", eventJson.GetProperty("eventType").GetString());
        Assert.NotEqual(default, eventJson.GetProperty("eventDate").GetDateTime());
        Assert.Equal("A01", eventJson.GetProperty("payload").GetProperty("PayloadType").GetString());
        Assert.Equal("mrn-1", eventJson.GetProperty("payload").GetProperty("MedicalRecordNumber").GetString());
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
    [InlineData("A03", typeof(AdtA03Payload))]
    [InlineData("A06", typeof(AdtA06Payload))]
    [InlineData("A07", typeof(AdtA07Payload))]
    [InlineData("A11", typeof(AdtA11Payload))]
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
        Assert.Equal("mrn-1", adt.PatientIdentifierList.Single().Id);
    }
}
