using System.Text;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services.Sftp;

[Trait("Category", "UnitTests")]
public class ConfigurableDelimitedParserTests
{
    private readonly ConfigurableDelimitedParser _parser;

    public ConfigurableDelimitedParserTests()
    {
        var logger = new Mock<ILogger<ConfigurableDelimitedParser>>().Object;
        _parser = new ConfigurableDelimitedParser(logger);
    }

    #region CanParse

    [Fact]
    public void CanParse_DelimitedParserTypeWithMappings_ReturnsTrue()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            ColumnMappings = new Dictionary<string, int> { { "PatientId", 0 } }
        };

        Assert.True(_parser.CanParse(default, default, ".csv", config));
    }

    [Fact]
    public void CanParse_NullConfig_ReturnsFalse()
    {
        Assert.False(_parser.CanParse(default, default, ".csv", null));
    }

    [Fact]
    public void CanParse_EmptyColumnMappings_ReturnsFalse()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            ColumnMappings = new Dictionary<string, int>()
        };

        Assert.False(_parser.CanParse(default, default, ".csv", config));
    }

    [Fact]
    public void CanParse_NonDelimitedParserType_ReturnsFalse()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "FixedWidth",
            ColumnMappings = new Dictionary<string, int> { { "PatientId", 0 } }
        };

        Assert.False(_parser.CanParse(default, default, ".csv", config));
    }

    #endregion

    #region ParseAsync - Core parsing

    [Fact]
    public async Task ParseAsync_PipeDelimitedWithMappings_ReturnsCorrectFields()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = "|",
            HasHeaderRow = true,
            ColumnMappings = new Dictionary<string, int>
            {
                { "PatientId", 0 },
                { "EncounterId", 1 },
                { "MRN", 2 }
            }
        };

        var content = """
            person_id|encntr_id|mrn
            12345|67890|MRN001
            11111|22222|MRN002
            """;

        var records = await ParseContentAsync(content, config);

        Assert.Equal(2, records.Count);
        Assert.Equal("12345", records[0]["PatientId"]);
        Assert.Equal("67890", records[0]["EncounterId"]);
        Assert.Equal("MRN001", records[0]["MRN"]);
        Assert.Equal("11111", records[1]["PatientId"]);
    }

    [Fact]
    public async Task ParseAsync_CommaDelimited_DefaultDelimiter()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = ",",
            HasHeaderRow = false,
            ColumnMappings = new Dictionary<string, int>
            {
                { "Name", 0 },
                { "Age", 1 }
            }
        };

        var content = "Alice,30\nBob,25";

        var records = await ParseContentAsync(content, config);

        Assert.Equal(2, records.Count);
        Assert.Equal("Alice", records[0]["Name"]);
        Assert.Equal("30", records[0]["Age"]);
    }

    [Fact]
    public async Task ParseAsync_NoHeaderRow_AllLinesProcessed()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = "|",
            HasHeaderRow = false,
            ColumnMappings = new Dictionary<string, int> { { "Id", 0 } }
        };

        var content = "AAA\nBBB\nCCC";

        var records = await ParseContentAsync(content, config);

        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task ParseAsync_WithHeaderRow_FirstLineSkipped()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = "|",
            HasHeaderRow = true,
            ColumnMappings = new Dictionary<string, int> { { "Id", 0 } }
        };

        var content = "header_id\nAAA\nBBB";

        var records = await ParseContentAsync(content, config);

        Assert.Equal(2, records.Count);
        Assert.Equal("AAA", records[0]["Id"]);
    }

    #endregion

    #region ParseAsync - Suffix stripping

    [Fact]
    public async Task ParseAsync_WithIdSuffixToStrip_RemovesSuffix()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = "|",
            HasHeaderRow = false,
            IdSuffixToStrip = ".00",
            ColumnMappings = new Dictionary<string, int>
            {
                { "PatientId", 0 },
                { "Name", 1 }
            }
        };

        var content = "12345.00|John Doe";

        var records = await ParseContentAsync(content, config);

        Assert.Single(records);
        Assert.Equal("12345", records[0]["PatientId"]);
        Assert.Equal("John Doe", records[0]["Name"]); // Non-matching suffix: untouched
    }

    #endregion

    #region ParseAsync - Error resilience

    [Fact]
    public async Task ParseAsync_OutOfRangeColumnIndex_SkipsFieldAndContinues()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = "|",
            HasHeaderRow = false,
            ColumnMappings = new Dictionary<string, int>
            {
                { "Id", 0 },
                { "Missing", 99 }  // out of range
            }
        };

        var content = "12345|data";

        var records = await ParseContentAsync(content, config);

        Assert.Single(records);
        Assert.Equal("12345", records[0]["Id"]);
        Assert.False(records[0].ContainsKey("Missing"));
    }

    [Fact]
    public async Task ParseAsync_AllMappingsOutOfRange_SkipsLine()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = "|",
            HasHeaderRow = false,
            ColumnMappings = new Dictionary<string, int>
            {
                { "Field1", 50 },
                { "Field2", 100 }
            }
        };

        var content = "only|two|fields";

        var records = await ParseContentAsync(content, config);

        // No fields could be mapped, so result count is 0 -> line is skipped (returns null)
        Assert.Empty(records);
    }

    [Fact]
    public async Task ParseAsync_NullConfig_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(
                new MemoryStream(), null, CancellationToken.None))
            {
            }
        });
    }

    [Fact]
    public async Task ParseAsync_BlankLines_Skipped()
    {
        var config = new FileParsingConfiguration
        {
            ParserType = "Delimited",
            Delimiter = "|",
            HasHeaderRow = false,
            ColumnMappings = new Dictionary<string, int> { { "Id", 0 } }
        };

        var content = "AAA\n\n\nBBB\n  \nCCC";

        var records = await ParseContentAsync(content, config);

        Assert.Equal(3, records.Count);
    }

    #endregion

    #region Helpers

    private async Task<List<Dictionary<string, string>>> ParseContentAsync(
        string content, FileParsingConfiguration config)
    {
        var results = new List<Dictionary<string, string>>();
        await foreach (var record in _parser.ParseAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(content)), config, CancellationToken.None))
        {
            results.Add(record);
        }
        return results;
    }

    #endregion
}
