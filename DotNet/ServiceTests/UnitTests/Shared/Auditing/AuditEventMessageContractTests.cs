using System.Text;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;

namespace UnitTests.Shared.Auditing;

/// <summary>
/// Checks AuditableEventOccurred against the JSON fixtures shared with the Java tests
/// (AuditEventMessageContractTest in Java/shared). The fixtures are what the .NET audit producer's
/// serialiser writes; the Java publisher must write the same, and the Audit service's deserialiser
/// must read what Java writes.
/// </summary>
[Trait("Category", "UnitTests")]
public class AuditEventMessageContractTests
{
    private static readonly DateTime EventDate = new(2026, 11, 23, 15, 4, 5, 123, DateTimeKind.Utc);

    [Fact]
    public void Serialize_UpdateEvent_MatchesSharedFixture()
    {
        AssertMatchesFixture("audit-event-update.json", Produce(UpdateEvent()));
    }

    [Fact]
    public void Serialize_CreateEvent_MatchesSharedFixture()
    {
        AssertMatchesFixture("audit-event-create.json", Produce(CreateEvent()));
    }

    [Fact]
    public void Consume_UpdateFixture_ReadsEveryField()
    {
        var consumed = Consume(File.ReadAllBytes(FixturePath("audit-event-update.json")));

        Assert.Equal("Hospital1", consumed.FacilityId);
        Assert.Equal("Validation", consumed.ServiceName);
        Assert.Null(consumed.CorrelationId);
        Assert.Equal(EventDate, consumed.EventDate);
        Assert.Equal(DateTimeKind.Utc, consumed.EventDate!.Value.Kind);
        Assert.Null(consumed.UserId);
        Assert.Equal("nurse@hospital.example.org", consumed.User);
        Assert.Equal(AuditEventType.Update, consumed.Action);
        Assert.Equal("Category", consumed.Resource);
        Assert.Equal("missing-required-element", consumed.ResourceId);
        Assert.Equal("Patient/123", consumed.PatientId);
        Assert.Null(consumed.Notes);

        Assert.NotNull(consumed.PropertyChanges);
        Assert.Equal(2, consumed.PropertyChanges.Count);

        var title = consumed.PropertyChanges[0];
        Assert.Equal("Title", title.PropertyName);
        Assert.Equal("Old", title.InitialPropertyValue);
        Assert.Equal("New", title.NewPropertyValue);
        Assert.Null(title.Entity);
        Assert.Null(title.RowKey);

        var apiKey = consumed.PropertyChanges[1];
        Assert.Equal("ApiKey", apiKey.PropertyName);
        Assert.Equal("[redacted]", apiKey.InitialPropertyValue);
        Assert.Equal("[redacted]", apiKey.NewPropertyValue);
        Assert.Null(apiKey.Entity);
        Assert.Null(apiKey.RowKey);
    }

    //Java's ISO_INSTANT text drops trailing zeros of the fraction in groups of three and the whole
    //fraction when it is zero, so Audit must read all three forms as the same kind of UTC instant.
    [Theory]
    [InlineData("2026-11-23T15:04:05.120Z", 120)]
    [InlineData("2026-11-23T15:04:05.100Z", 100)]
    [InlineData("2026-11-23T15:04:05Z", 0)]
    public void Consume_JavaEventDate_ReadsTrailingZeroAndWholeSecondForms(string eventDate, int millisecond)
    {
        var json = $$"""{"ServiceName":"Validation","EventDate":"{{eventDate}}","Action":"Update"}""";

        var consumed = Consume(Encoding.UTF8.GetBytes(json));

        var expected = new DateTime(2026, 11, 23, 15, 4, 5, millisecond, DateTimeKind.Utc);
        Assert.NotNull(consumed.EventDate);
        Assert.Equal(expected, consumed.EventDate.Value);
        //DateTime equality ignores Kind, and ToUniversalTime() is a no-op on a UTC host either way.
        Assert.Equal(DateTimeKind.Utc, consumed.EventDate.Value.Kind);
        Assert.Equal(consumed.EventDate.Value, consumed.EventDate.Value.ToUniversalTime());
    }

    private static AuditEventMessage UpdateEvent() => new()
    {
        FacilityId = "Hospital1",
        ServiceName = "Validation",
        CorrelationId = null,
        EventDate = EventDate,
        UserId = null,
        User = "nurse@hospital.example.org",
        Action = AuditEventType.Update,
        Resource = "Category",
        ResourceId = "missing-required-element",
        PatientId = "Patient/123",
        PropertyChanges =
        [
            new PropertyChangeModel("Title", "Old", "New"),
            new PropertyChangeModel("ApiKey", "[redacted]", "[redacted]")
        ],
        Notes = null
    };

    private static AuditEventMessage CreateEvent() => new()
    {
        FacilityId = null,
        ServiceName = "MeasureEval",
        CorrelationId = null,
        EventDate = EventDate,
        UserId = null,
        User = null,
        Action = AuditEventType.Create,
        Resource = "MeasureDefinition",
        ResourceId = "measure-1",
        PatientId = null,
        PropertyChanges = null,
        Notes = "measure-1 created"
    };

    //the serialiser the audit producer is built with - KafkaProducerFactory.CreateAuditEventProducer.
    private static byte[] Produce(AuditEventMessage message) =>
        new JsonWithFhirMessageSerializer<AuditEventMessage>().Serialize(message, Context());

    //the deserialiser Audit's AuditEventListener consumes with.
    private static AuditEventMessage Consume(byte[] payload)
    {
        var consumed = new JsonWithFhirMessageDeserializer<AuditEventMessage>()
            .Deserialize(payload, false, Context());

        Assert.NotNull(consumed);
        return consumed;
    }

    private static void AssertMatchesFixture(string fixture, byte[] produced)
    {
        var expected = JsonNode.Parse(File.ReadAllText(FixturePath(fixture)));
        var actual = JsonNode.Parse(Encoding.UTF8.GetString(produced));

        Assert.True(JsonNode.DeepEquals(expected, actual),
                    $"Produced JSON does not match {fixture}.{Environment.NewLine}" +
                    $"Expected: {expected?.ToJsonString()}{Environment.NewLine}" +
                    $"Actual:   {actual?.ToJsonString()}");
    }

    private static string FixturePath(string fixture)
    {
        return Path.Combine(AppContext.BaseDirectory, "Resources", "Audit", fixture);
    }

    private static SerializationContext Context() =>
        new(MessageComponentType.Value, nameof(KafkaTopic.AuditableEventOccurred));
}
