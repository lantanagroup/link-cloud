using System.Text;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services.Sftp;

[Trait("Category", "UnitTests")]
public class CernerCclExtractParserTests
{
    private readonly CernerCclExtractParser _parser;

    public CernerCclExtractParserTests()
    {
        var logger = new Mock<ILogger<CernerCclExtractParser>>().Object;
        _parser = new CernerCclExtractParser(logger);
    }

    #region CanParse

    [Fact]
    public void CanParse_CensusCernerCclExtractDatFile_ReturnsTrue()
    {
        var result = _parser.CanParse(
            SftpAcquisitionType.Census,
            SftpAcquisitionSubType.CernerCCLExtract,
            ".dat",
            null);

        Assert.True(result);
    }

    [Theory]
    [InlineData(SftpAcquisitionType.Resources, SftpAcquisitionSubType.CernerCCLExtract, ".dat")]
    [InlineData(SftpAcquisitionType.Census, SftpAcquisitionSubType.None, ".dat")]
    [InlineData(SftpAcquisitionType.Census, SftpAcquisitionSubType.CernerCCLExtract, ".csv")]
    public void CanParse_WrongTypeSubTypeOrExtension_ReturnsFalse(
        SftpAcquisitionType type, SftpAcquisitionSubType subType, string ext)
    {
        Assert.False(_parser.CanParse(type, subType, ext, null));
    }

    #endregion

    #region ParseAsync - Core parsing

    [Fact]
    public async Task ParseAsync_ValidFileWithHeader_ParsesAllDataRows()
    {
        var content = """
            person_id|encntr_id|facility|unit|room|bed|fin|mrn|pat_nam|enc_status|enc_type|admit_dt|disch_dt
            12345.00|67890.00|FacA|UnitB|101|A|FIN001|MRN001|Doe, John|Active|Inpatient|20230707130643|20230710080000
            11111.00|22222.00|FacA|UnitC|102|B|FIN002|MRN002|Smith, Jane|Discharged|Emergency|20230801093015|20230801170000
            """;

        var encounters = await ParseContentAsync(content);

        Assert.Equal(2, encounters.Count);

        Assert.Equal("12345", encounters[0].PatientId);
        Assert.Equal("67890", encounters[0].EncounterId);
        Assert.Equal("FIN001", encounters[0].FinNumber);
        Assert.Equal("MRN001", encounters[0].MRN);
        Assert.Equal("Active", encounters[0].EncounterStatus);
        Assert.Equal("Inpatient", encounters[0].EncounterType);
        Assert.Equal(new DateTime(2023, 7, 7, 13, 6, 43, DateTimeKind.Utc), encounters[0].AdmitDate);

        Assert.Equal("11111", encounters[1].PatientId);
        Assert.Equal("22222", encounters[1].EncounterId);
    }

    [Fact]
    public async Task ParseAsync_FileWithoutHeader_FirstDataLineNotSkipped()
    {
        // First line doesn't start with "person_id", so it should be treated as data
        var content = "99999.00|88888.00|Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230101120000|";

        var encounters = await ParseContentAsync(content);

        Assert.Single(encounters);
        Assert.Equal("99999", encounters[0].PatientId);
    }

    [Fact]
    public async Task ParseAsync_EmptyFile_ReturnsNoResults()
    {
        var encounters = await ParseContentAsync("");

        Assert.Empty(encounters);
    }

    [Fact]
    public async Task ParseAsync_OnlyHeaderRow_ReturnsNoResults()
    {
        var content = "person_id|encntr_id|facility|unit|room|bed|fin|mrn|pat_nam|enc_status|enc_type|admit_dt|disch_dt";

        var encounters = await ParseContentAsync(content);

        Assert.Empty(encounters);
    }

    #endregion

    #region ParseAsync - ID cleaning (.00 suffix)

    [Fact]
    public async Task ParseAsync_IdsWithCernerSuffix_StripsPointZeroZero()
    {
        var content = "12345.00|67890.00|Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230101120000|";

        var encounters = await ParseContentAsync(content);

        Assert.Equal("12345", encounters[0].PatientId);
        Assert.Equal("67890", encounters[0].EncounterId);
    }

    [Fact]
    public async Task ParseAsync_IdsWithoutSuffix_PreservedAsIs()
    {
        var content = "ABC123|ENC456|Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230101120000|";

        var encounters = await ParseContentAsync(content);

        Assert.Equal("ABC123", encounters[0].PatientId);
        Assert.Equal("ENC456", encounters[0].EncounterId);
    }

    #endregion

    #region ParseAsync - Error resilience

    [Fact]
    public async Task ParseAsync_LineWithInsufficientColumns_SkipsLineAndContinues()
    {
        var content = """
            person_id|encntr_id|facility|unit|room|bed|fin|mrn|pat_nam|enc_status|enc_type|admit_dt|disch_dt
            12345.00|67890.00|only|three|fields
            11111.00|22222.00|FacA|UnitC|102|B|FIN002|MRN002|Smith|Discharged|Emergency|20230801093015|
            """;

        var encounters = await ParseContentAsync(content);

        Assert.Single(encounters);
        Assert.Equal("11111", encounters[0].PatientId);
    }

    [Fact]
    public async Task ParseAsync_EmptyPatientIdOrEncounterId_SkipsLine()
    {
        var content = """
            person_id|encntr_id|facility|unit|room|bed|fin|mrn|pat_nam|enc_status|enc_type|admit_dt|disch_dt
            |67890.00|Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230101120000|
            12345.00||Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230101120000|
            11111.00|22222.00|Fac|Unit|102|B|FIN2|MRN2|Name2|Active|IP|20230201120000|
            """;

        var encounters = await ParseContentAsync(content);

        Assert.Single(encounters);
        Assert.Equal("11111", encounters[0].PatientId);
    }

    [Fact]
    public async Task ParseAsync_BlankLines_SkippedGracefully()
    {
        var content = "person_id|encntr_id|facility\n\n\n12345.00|67890.00|Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230101120000|\n\n";

        var encounters = await ParseContentAsync(content);

        Assert.Single(encounters);
    }

    #endregion

    #region ParseAsync - Date parsing

    [Fact]
    public async Task ParseAsync_CernerDateFormat_ParsedCorrectlyAsUtc()
    {
        var content = "12345|67890|Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230707130643|";

        var encounters = await ParseContentAsync(content);

        var expected = new DateTime(2023, 7, 7, 13, 6, 43, DateTimeKind.Utc);
        Assert.Equal(expected, encounters[0].AdmitDate);
        Assert.Equal(DateTimeKind.Utc, encounters[0].AdmitDate.Kind);
    }

    [Fact]
    public async Task ParseAsync_EmptyAdmitDate_DefaultsToMinValue()
    {
        var content = "12345|67890|Fac|Unit|101|A|FIN|MRN|Name|Active|IP||";

        var encounters = await ParseContentAsync(content);

        Assert.Equal(DateTime.MinValue, encounters[0].AdmitDate);
    }

    #endregion

    #region ParseAsync - Cancellation

    [Fact]
    public async Task ParseAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        var content = "12345|67890|Fac|Unit|101|A|FIN|MRN|Name|Active|IP|20230101120000|";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(
                CreateStream(content), null, cts.Token))
            {
            }
        });
    }

    #endregion

    #region Helpers

    private async Task<List<CernerEncounters>> ParseContentAsync(string content)
    {
        var results = new List<CernerEncounters>();
        await foreach (var encounter in _parser.ParseAsync(
            CreateStream(content), null, CancellationToken.None))
        {
            results.Add(encounter);
        }
        return results;
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    #endregion
}
