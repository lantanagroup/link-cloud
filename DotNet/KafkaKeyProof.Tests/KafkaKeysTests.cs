using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class KafkaKeysTests
{
    [Fact]
    public void CanonicalKey_EscapesQuotesSlashesAndKeepsNonAscii()
    {
        var key = KafkaKeys.ForPatient("a/b\"c\\d", "患者");
        Assert.Equal("{\"facilityId\":\"a/b\\\"c\\\\d\",\"patientId\":\"患者\"}", key);
        Assert.True(LinkMessageKey.TryDeserialize(" { \"patientId\" : \"患者\" , \"facilityId\" : \"a/b\\\"c\\\\d\" } ", out var parsed));
        Assert.Equal("a/b\"c\\d", parsed!.FacilityId);
        Assert.Equal("患者", parsed.PatientId);
    }

    [Fact]
    public void ForPatient_EmitsCanonicalJson()
    {
        Assert.Equal("{\"facilityId\":\"facility-1\",\"patientId\":\"patient-9\"}", KafkaKeys.ForPatient("facility-1", "patient-9"));
        Assert.Equal("{\"facilityId\":\"  facility-1  \",\"patientId\":\" patient-9 \"}", KafkaKeys.ForPatient("  facility-1  ", " patient-9 "));
    }

    [Theory]
    [InlineData(null, "patient")]
    [InlineData("", "patient")]
    [InlineData("facility", null)]
    [InlineData("facility", "")]
    public void ForPatient_RejectsEmptyParts(string? facilityId, string? patientId)
    {
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForPatient(facilityId, patientId));
    }

    [Fact]
    public void ForFacility_RejectsBlankAndTrims()
    {
        Assert.Equal("{\"facilityId\":\"facility-1\"}", KafkaKeys.ForFacility("facility-1"));
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForFacility(string.Empty));
    }

    [Fact]
    public void ForPatient_RejectsUnpairedSurrogatesAndKeepsPairs()
    {
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForPatient("\uD800", "patient"));
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForPatient("facility", "\uDFFF"));
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForPatient("a\uD800b", "patient"));
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForPatient("ok\uD83D", "patient"));
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForFacility("\uDC00"));
        Assert.Throws<ArgumentException>(() => KafkaKeys.ForService("\uD800"));
        Assert.Equal(
            "{\"facilityId\":\"\U0001F600\",\"patientId\":\"patient-proof\"}",
            KafkaKeys.ForPatient("\U0001F600", "patient-proof"));
    }

    [Fact]
    public void ForAudit_UsesPatientThenFacilityThenService()
    {
        Assert.Equal("{\"facilityId\":\"fac\",\"patientId\":\"pat\"}", KafkaKeys.ForAudit("fac", "pat", "QueryDispatch"));
        Assert.Equal("{\"facilityId\":\"fac\"}", KafkaKeys.ForAudit("fac", null, "QueryDispatch"));
        Assert.Equal("QueryDispatch", KafkaKeys.ForAudit(null, null, "QueryDispatch"));
    }
}
