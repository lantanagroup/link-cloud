using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class PayloadSubmittedRedriveTests
{
    private static readonly Guid ScheduleId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Redrive_UsesTheSameReportKeyAsTheMainCompletion()
    {
        var reportKey = KafkaKeys.ForReport("facility-1", ScheduleId);
        var patientKey = KafkaKeys.ForPatient("facility-1", "patient-9");
        var value = "{\"facilityId\":\"facility-1\",\"patientId\":\"patient-9\",\"reportScheduleId\":\"11111111-2222-3333-4444-555555555555\"}";

        Assert.Equal(reportKey, PayloadSubmittedRedrive.Key(reportKey, value));
        Assert.Equal(reportKey, PayloadSubmittedRedrive.Key(patientKey, value));
        Assert.NotEqual(patientKey, PayloadSubmittedRedrive.Key(patientKey, value));

        var otherSchedule = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var otherValue = "{\"facilityId\":\"facility-1\",\"reportScheduleId\":\"22222222-3333-4444-5555-666666666666\"}";
        Assert.Equal(KafkaKeys.ForReport("facility-1", otherSchedule), PayloadSubmittedRedrive.Key(patientKey, otherValue));
        Assert.NotEqual(reportKey, PayloadSubmittedRedrive.Key(patientKey, otherValue));
    }

    [Fact]
    public void Applies_OnlyToPayloadSubmitted()
    {
        Assert.True(PayloadSubmittedRedrive.Applies("PayloadSubmitted"));
        Assert.False(PayloadSubmittedRedrive.Applies("PatientEvent"));
        Assert.False(PayloadSubmittedRedrive.Applies(null));
    }

    [Fact]
    public void Key_RequiresFacilityAndSchedule()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PayloadSubmittedRedrive.Key(KafkaKeys.ForPatient("facility-1", "patient-9"), "{\"facilityId\":\"facility-1\"}"));
    }
}
