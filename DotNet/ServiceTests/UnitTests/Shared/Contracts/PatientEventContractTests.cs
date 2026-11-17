using static UnitTests.Shared.Contracts.KafkaContract;
using AdminBffMessage = LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration.PatientEventMessage;
using CensusPatientEvent = LantanaGroup.Link.Census.Application.Models.Messages.PatientEvent;
using QueryDispatchPatientEvent = LantanaGroup.Link.QueryDispatch.Application.Models.PatientEventValue;
using ReportPatientEvent = LantanaGroup.Link.Report.Models.PatientEventValue;

namespace UnitTests.Shared.Contracts;

/// <summary>
/// Pins the PatientEvent message. Census produces it, Admin BFF's integration endpoint produces the same
/// shape, and Report and QueryDispatch each read it with their own class.
/// </summary>
[Trait("Category", "UnitTests")]
public class PatientEventContractTests
{
    private const string Fixture = "patient-event.json";
    private const string WithTrackingIdFixture = "patient-event-with-tracking-id.json";

    [Fact]
    public void Serialize_CensusEventWithoutTrackingId_OmitsTrackingId()
    {
        var value = new CensusPatientEvent
        {
            PatientId = "pat-001",
            EventType = "Discharge"
        };

        AssertProducesFixture(value, Fixture);
    }

    [Fact]
    public void Serialize_CensusEventWithTrackingId_MatchesFixture()
    {
        var value = new CensusPatientEvent
        {
            PatientId = "pat-001",
            EventType = "Admit",
            ReportTrackingId = "3f6a1c2e-8b4d-4e7a-9c15-2d0b7e9f4a61"
        };

        AssertProducesFixture(value, WithTrackingIdFixture);
    }

    [Fact]
    public void Serialize_AdminBffMessage_MatchesFixture()
    {
        var message = new AdminBffMessage
        {
            PatientId = "pat-001",
            EventType = "Discharge"
        };

        // Admin BFF produces through IProducer<string, object>, so the runtime type decides the shape.
        AssertProducesFixture<object>(message, Fixture);
    }

    [Theory]
    [InlineData(Fixture, "Discharge")]
    [InlineData(WithTrackingIdFixture, "Admit")]
    public void Deserialize_ReportConsumer_ReadsPatientAndEventType(string fixture, string eventType)
    {
        var consumed = Consume<ReportPatientEvent>(fixture);

        Assert.Equal("pat-001", consumed.PatientId);
        Assert.Equal(eventType, consumed.EventType);
    }

    [Theory]
    [InlineData(Fixture, "Discharge")]
    [InlineData(WithTrackingIdFixture, "Admit")]
    public void Deserialize_QueryDispatchConsumer_ReadsPatientAndEventType(string fixture, string eventType)
    {
        var consumed = Consume<QueryDispatchPatientEvent>(fixture);

        Assert.Equal("pat-001", consumed.PatientId);
        Assert.Equal(eventType, consumed.EventType);
        Assert.True(consumed.IsValid());
    }
}
