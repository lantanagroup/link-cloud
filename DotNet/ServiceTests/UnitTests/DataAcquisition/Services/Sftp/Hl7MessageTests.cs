using System.Text;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services.Sftp;

[Trait("Category", "UnitTests")]
public class Hl7MessageTests
{
    private const string Sample =
        "MSH|^~\\&|EHR|HOSP|LINK|NHSN|20260102103000-0500||ADT^A01^ADT_A01|CTRL1|P|2.5\r" +
        "EVN|A01|20260102103000\r" +
        "PID|1|EXT1|MRN1^^^HOSP^MR~fhir-1^^^LINK^FHIR~SSN1^^^US^SS|ALT1|DOE^JANE|||||||||||||ACCT1^^^HOSP\r" +
        "PV1|1|I|4E^401^A||||||||||||||||VISIT1^^^HOSP^VN|||||||||||||||||||||||||20260102100000|\"\"\r" +
        "OBX|1|ST|OTHER||ignored\r" +
        "OBX|2|ST|FHIRID^Patient Id||Patient/fhir-2\r";

    private static Hl7Message Message(string content = Sample) =>
        Hl7Message.Parse(content.Split('\r', StringSplitOptions.RemoveEmptyEntries), 1);

    [Theory]
    [InlineData("MSH-7", "20260102103000-0500")]
    [InlineData("MSH-9.2", "A01")]
    [InlineData("MSH.9.2", "A01")]
    [InlineData("msh-10", "CTRL1")]
    [InlineData("PID-2", "EXT1")]
    [InlineData("PID-3", "MRN1^^^HOSP^MR")]
    [InlineData("PID-3.1", "MRN1")]
    [InlineData("PID-3[2].1", "fhir-1")]
    [InlineData("PID-3[5=MR].1", "MRN1")]
    [InlineData("PID-3[5=fhir].1", "fhir-1")]
    [InlineData("PID-18.1", "ACCT1")]
    [InlineData("PV1-3", "4E^401^A")]
    [InlineData("PV1-19.1", "VISIT1")]
    [InlineData("OBX-5", "ignored")]
    [InlineData("OBX[2]-5", "Patient/fhir-2")]
    [InlineData("OBX[3.1=FHIRID]-5", "Patient/fhir-2")]
    [InlineData("OBX[3=OTHER]-5", "ignored")]
    public void GetValue_ReadsLocation(string location, string expected)
    {
        Assert.True(Hl7Path.TryParse(location, out var path));
        Assert.Equal(expected, Message().GetValue(path!));
    }

    [Theory]
    [InlineData("ZZZ-1")]
    [InlineData("PID-99")]
    [InlineData("PID-3[9].1")]
    [InlineData("PID-3[5=NOPE].1")]
    [InlineData("OBX[3.1=NOPE]-5")]
    [InlineData("PV1-45")] // the HL7 explicit null, ""
    public void GetValue_AbsentOrNull_ReturnsNull(string location)
    {
        Assert.Null(Message().GetValue(Hl7Path.Parse(location)));
    }

    [Theory]
    [InlineData("PID")]
    [InlineData("PID-")]
    [InlineData("PID-0")]
    [InlineData("PATIENT-1")]
    [InlineData("PID-3[]")]
    [InlineData("PID-3[x=y]")]
    [InlineData("PID-3.1.2.3")]
    [InlineData("OBX[a]-5")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_InvalidLocation_ReturnsFalse(string? location)
    {
        Assert.False(Hl7Path.TryParse(location, out _));
    }

    [Fact]
    public void GetValue_UsesTheMessagesOwnEncodingCharacters()
    {
        var message = Message("MSH#*~\\&#EHR\rPID#1#A*B~C*D#X\\S\\Y");

        Assert.Equal("B", message.GetValue(Hl7Path.Parse("PID-2.2")));
        Assert.Equal("C", message.GetValue(Hl7Path.Parse("PID-2[2].1")));
        Assert.Equal("X*Y", message.GetValue(Hl7Path.Parse("PID-3")));
    }

    [Fact]
    public void GetIdentifierList_ReadsEveryRepetition()
    {
        var identifiers = Message().GetIdentifierList("PID", 3);

        Assert.Equal(3, identifiers.Count);
        Assert.Equal(("MRN1", "HOSP", "MR"), identifiers[0]);
        Assert.Equal(("SSN1", "US", "SS"), identifiers[2]);
    }

    [Fact]
    public void ParseTimestamp_WithOffset_IsConvertedToUtc()
    {
        var result = Hl7Message.ParseTimestamp("20260102103000-0500");

        Assert.Equal(new DateTime(2026, 1, 2, 15, 30, 0), result);
        Assert.Equal(DateTimeKind.Utc, result!.Value.Kind);
    }

    [Theory]
    [InlineData("20260102103000", 2026, 1, 2, 10, 30, 0)]
    [InlineData("20260102103000.1234", 2026, 1, 2, 10, 30, 0)]
    [InlineData("202601021030", 2026, 1, 2, 10, 30, 0)]
    [InlineData("2026010210", 2026, 1, 2, 10, 0, 0)]
    [InlineData("20260102", 2026, 1, 2, 0, 0, 0)]
    public void ParseTimestamp_WithoutOffset_IsKeptAsSent(string value, int y, int mo, int d, int h, int mi, int s)
    {
        var result = Hl7Message.ParseTimestamp(value);

        Assert.Equal(new DateTime(y, mo, d, h, mi, s), result);
        Assert.Equal(DateTimeKind.Unspecified, result!.Value.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("20261399")]
    public void ParseTimestamp_Invalid_ReturnsNull(string? value)
    {
        Assert.Null(Hl7Message.ParseTimestamp(value));
    }

    [Fact]
    public void Map_ReadsTheAcquiredElements()
    {
        var entry = AdtEventMapper.Map(Message(), "A01", Hl7Path.Parse("PID-3[5=MR].1"), Hl7Path.Parse("PV1-19.1"));

        Assert.Equal("MRN1", entry.MedicalRecordNumber);
        Assert.Equal("VISIT1", entry.EncounterId);
        Assert.Equal(new DateTime(2026, 1, 2, 15, 30, 0), entry.MessageDateTime);
        Assert.Equal("A01", entry.MessageType);
        Assert.Equal("EXT1", entry.PatientId);
        Assert.Equal(3, entry.PatientIdentifierList.Count);
        Assert.Equal("ALT1", entry.AlternatePatientId);
        Assert.Equal("ACCT1", entry.PatientAccountNumber);
        Assert.Equal("I", entry.PatientClass);
        Assert.Equal("4E^401^A", entry.Location);
        Assert.Equal(new DateTime(2026, 1, 2, 10, 0, 0), entry.AdmitDateTime);
        Assert.Null(entry.DischargeDateTime);
        Assert.Null(entry.Merge);
    }

    [Fact]
    public void Map_A18_ReadsTheMrgSegment()
    {
        var message = Message("MSH|^~\\&|EHR\rPID|1||MRN2^^^HOSP^MR\rMRG|MRN1^^^HOSP^MR~OLD2||ACCT1|PRIOR1|VISIT1|ALTV1\r");

        var merge = AdtEventMapper.Map(message, "A18", Hl7Path.Parse("PID-3.1"), Hl7Path.Parse("PV1-19.1")).Merge;

        Assert.NotNull(merge);
        Assert.Equal(["MRN1", "OLD2"], merge.PriorPatientIdentifierList.Select(x => x.Id));
        Assert.Equal("ACCT1", merge.PriorPatientAccountNumber);
        Assert.Equal("PRIOR1", merge.PriorPatientId);
        Assert.Equal("VISIT1", merge.PriorVisitNumber);
        Assert.Equal("ALTV1", merge.PriorAlternateVisitId);
    }

    [Fact]
    public void Map_MrgOnAnotherMessageType_IsLeftOut()
    {
        var message = Message("MSH|^~\\&|EHR\rPID|1||MRN2\rMRG|MRN1\r");

        Assert.Null(AdtEventMapper.Map(message, "A01", Hl7Path.Parse("PID-3.1"), Hl7Path.Parse("PV1-19.1")).Merge);
    }

    [Fact]
    public void GetMessageType_FallsBackToEvn()
    {
        Assert.Equal("A02", AdtEventMapper.GetMessageType(Message("MSH|^~\\&|EHR||||||ADT\rEVN|a02\r")));
        Assert.Null(AdtEventMapper.GetMessageType(Message("MSH|^~\\&|EHR\r")));
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task ParseAsync_SplitsMessagesInOrder_WithAnySegmentTerminator(string terminator)
    {
        var content = string.Join(terminator, "MSH|^~\\&|EHR||||||ADT^A01|C1", "PID|1", "MSH|^~\\&|EHR||||||ADT^A03|C2", "PID|2", "");

        var messages = await ParseAsync(content);

        Assert.Equal(["C1", "C2"], messages.Select(AdtEventMapper.GetMessageControlId));
        Assert.Equal([1, 2], messages.Select(m => m.Position));
    }

    [Fact]
    public async Task ParseAsync_HandlesBatchEnvelopesAndMllpFraming()
    {
        var content =
            "FHS|^~\\&|EHR\rBHS|^~\\&|EHR\r" +
            "\u000BMSH|^~\\&|EHR||||||ADT^A01|C1\rPID|1\r\u001C\r" +
            "\u000BMSH|^~\\&|EHR||||||ADT^A03|C2\rPID|2\r\u001C\r" +
            "BTS|2\rFTS|1\r";

        var messages = await ParseAsync(content);

        Assert.Equal(["C1", "C2"], messages.Select(AdtEventMapper.GetMessageControlId));
        Assert.False(messages[1].HasSegment("BTS"));
    }

    [Fact]
    public async Task ParseAsync_NoMessages_ReturnsNothing()
    {
        Assert.Empty(await ParseAsync("not\nan hl7\nfile"));
    }

    [Theory]
    [InlineData(SftpAcquisitionType.Census, SftpAcquisitionSubType.HL7ADT, true)]
    [InlineData(SftpAcquisitionType.Census, SftpAcquisitionSubType.CernerCCLExtract, false)]
    [InlineData(SftpAcquisitionType.Resources, SftpAcquisitionSubType.HL7ADT, false)]
    public void CanParse_OnlyHl7AdtCensus(SftpAcquisitionType type, SftpAcquisitionSubType subType, bool expected)
    {
        Assert.Equal(expected, new Hl7AdtFileParser().CanParse(type, subType, ".hl7", null));
    }

    private static async Task<List<Hl7Message>> ParseAsync(string content)
    {
        var messages = new List<Hl7Message>();
        await foreach (var message in new Hl7AdtFileParser().ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)), null, CancellationToken.None))
        {
            messages.Add(message);
        }

        return messages;
    }
}
