using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class KafkaKeyLegacyTests
{
    [Fact]
    public void ReadsLegacyResourceKey()
    {
        const string key = "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}";
        Assert.True(KafkaKeyLegacy.TryReadFacility(key, out var facilityId));
        Assert.Equal("fac", facilityId);
        Assert.True(KafkaKeyLegacy.TryReadPatient(key, out var patientId));
        Assert.Equal("pat", patientId);
    }

    [Fact]
    public void ReadsLegacyReportKeyIgnoringCase()
    {
        var reportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = "{\"FacilityId\":\"fac\",\"ReportScheduleId\":\"" + reportId + "\"}";
        Assert.True(KafkaKeyLegacy.TryReadReportScheduleId(key, out var parsed));
        Assert.Equal(reportId, parsed);
        Assert.Equal("fac", KafkaIdentity.Facility(null, key));
        Assert.Equal(reportId, KafkaIdentity.ReportSchedule(null, key));
    }

    [Fact]
    public void PlainFacilityKeyIsLegacy()
    {
        Assert.Equal("fac", KafkaIdentity.RequireFacility(null, "fac"));
        Assert.Equal("fac", KafkaIdentity.RequireFacility(null, "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}"));
        Assert.Equal("pat", KafkaIdentity.RequirePatient(null, "{\"facilityId\":\"fac\",\"patientId\":\"pat\"}"));
    }

    [Fact]
    public void ValueWinsOverLegacyKey()
    {
        Assert.Equal("from-value", KafkaIdentity.Facility("from-value", "{\"facilityId\":\"from-key\"}"));
    }

    [Fact]
    public void InvalidKeyDoesNotHideValueIds()
    {
        Assert.Equal("from-value", KafkaIdentity.Facility("from-value", "not-json-and-not-a-facility-we-need"));
        Assert.Null(KafkaIdentity.Patient(null, "{\"facilityId\":\"fac\"}"));
    }
}
