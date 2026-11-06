using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// What AuditableEventOccurred carries in its Action field, through the serialiser the audit
    /// producer is actually built with and the deserialiser the Audit service consumes with.
    /// </summary>
    /// <remarks>
    /// LEGLINK-1630 put a JsonStringEnumConverter on <see cref="AuditEventType"/> so the topic reads
    /// as "Action":"Update" rather than an ordinal. Nothing else makes that happen: the producer's
    /// options come from .ForFhir(ModelInfo.ModelInspector) and the consumer's from
    /// LinkFhirSerializerOptions.ForFhirLenientSerialization, and neither registers an enum
    /// converter of its own - so the attribute is the whole mechanism, and these tests fail if it is
    /// removed, or if a member is reordered or inserted mid-enum.
    /// <para>
    /// Both forms are tested because both are on the topic at once. A deployment reads the integer
    /// form from messages produced before this build and from anything parked on
    /// AuditableEventOccurred-Retry or -Error, which nothing replays into a newer format.
    /// </para>
    /// </remarks>
    [Trait("Category", "UnitTests")]
    public class AuditEventTypeContractTests
    {
        [Fact]
        public void Action_CrossesTheWireByName()
        {
            var json = JsonNode.Parse(Encoding.UTF8.GetString(Produce(AnEvent(AuditEventType.Update))));

            var action = json?["Action"];
            Assert.NotNull(action);

            //the kind, not just the value: an ordinal reads as 1 here and would make the enum's
            //declaration order the wire contract for every new message, not only the old ones.
            Assert.Equal(JsonValueKind.String, action.GetValueKind());
            Assert.Equal("Update", action.GetValue<string>());
        }

        [Fact]
        public void TheNameForm_IsReadByTheConsumersDeserialiser()
        {
            //the two halves are configured separately, and the consumer's options register no enum
            //converter of their own - so Audit can only read a name because of the attribute. This is
            //the deploy-order constraint: a name reaching a consumer that cannot read it dead-letters
            //to AuditableEventOccurred-Error, and nothing replays that.
            var consumed = Consume(Encoding.UTF8.GetBytes("{\"Action\":\"SoftDelete\"}"));

            Assert.Equal(AuditEventType.SoftDelete, consumed.Action);
        }

        [Theory]
        [InlineData(0, AuditEventType.Create)]
        [InlineData(1, AuditEventType.Update)]
        [InlineData(2, AuditEventType.Delete)]
        [InlineData(3, AuditEventType.Query)]
        [InlineData(4, AuditEventType.Submit)]
        [InlineData(5, AuditEventType.Restore)]
        [InlineData(6, AuditEventType.SoftDelete)]
        public void TheIntegerForm_IsStillRead(int ordinal, AuditEventType expected)
        {
            //allowIntegerValues is the converter's default and is what keeps messages produced
            //before LEGLINK-1630 readable. Turning it off would deserialise none of these.
            var consumed = Consume(Encoding.UTF8.GetBytes($"{{\"Action\":{ordinal}}}"));

            Assert.Equal(expected, consumed.Action);
        }

        [Theory]
        [InlineData(AuditEventType.Create, 0)]
        [InlineData(AuditEventType.Update, 1)]
        [InlineData(AuditEventType.Delete, 2)]
        [InlineData(AuditEventType.Query, 3)]
        [InlineData(AuditEventType.Submit, 4)]
        [InlineData(AuditEventType.Restore, 5)]
        [InlineData(AuditEventType.SoftDelete, 6)]
        public void DeclaredOrder_StaysTheContractForTheIntegerForm(AuditEventType action, int ordinal)
        {
            //reading the integer form means the declared order is still load-bearing: inserting or
            //reordering a member silently reinterprets every message already on the topic, with no
            //deserialisation error anywhere. Append new values below SoftDelete.
            Assert.Equal(ordinal, (int)action);
        }

        private static AuditEventMessage AnEvent(AuditEventType action) => new()
        {
            FacilityId = "Hospital1",
            ServiceName = "QueryDispatch",
            EventDate = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc),
            Action = action,
            Resource = "QueryDispatchConfiguration",
            ResourceId = "6f1c2a9e-4b7d-4c1e-9a53-2d8e7f0b1c34",
            User = "nurse@hospital.example.org"
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

        private static SerializationContext Context() =>
            new(MessageComponentType.Value, nameof(KafkaTopic.AuditableEventOccurred));
    }
}
