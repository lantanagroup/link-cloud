using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Terminology;

public class CodeGroupCacheServiceTests
{
    private readonly Mock<ILogger<CodeGroupCacheService>> _loggerMock;
    private readonly TerminologyConfig _config;

    public CodeGroupCacheServiceTests()
    {
        _loggerMock = new Mock<ILogger<CodeGroupCacheService>>();
        _config = new TerminologyConfig { Path = "/test/path" };
    }

    // Mirrors the reader LoadCache builds: the optional trailing status column means a
    // 2-column CSV has no field at index 2, so missing fields must not be treated as errors.
    private static CsvReader CreateCsvReader(string csvData)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
        return new CsvReader(new StringReader(csvData), config);
    }

    [Fact]
    public async Task LoadCache_ShouldAttemptToLoadFilesFromEachDirectory()
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        // Create test service with mocked file system methods
        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        mockService
            .Setup(s => s.DirectoryExists(It.IsAny<string>()))
            .Returns(true);

        // Mock directories to return
        var testDirectories = new[]
        {
            "/test/path/dir1",
            "/test/path/dir2",
            "/test/path/dir3"
        };

        mockService
            .Setup(s => s.GetDirectories(It.IsAny<string>()))
            .Returns(testDirectories);

        // Setup mock responses for GetFiles to simulate both JSON and CSV files exist
        mockService
            .Setup(s => s.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(new[] { "test.json" });

        mockService
            .Setup(s => s.GetFiles(It.IsAny<string>(), "*.csv"))
            .Returns(new[] { "test.csv" });

        // Mock file content reading to return empty content
        mockService
            .Setup(s => s.ReadAllTextAsync("test.json"))
            .ReturnsAsync("{ \"resourceType\": \"ValueSet\", \"id\": \"valueset\" }");

        mockService
            .Setup(s => s.ReadAllTextAsync("test.csv"))
            .ReturnsAsync("system,code,display\r\n" +
                          "http://somesystem.com,abcd,Some Code\r\n");

        // Act
        await mockService.Object.LoadCache();

        mockService.Verify(
            s => s.DirectoryExists(mockConfig.Object.Value.Path),
            Times.Once);

        // Assert
        // Verify that GetDirectories was called once with the config path
        mockService.Verify(
            s => s.GetDirectories(mockConfig.Object.Value.Path),
            Times.Once);

        // Verify that for each directory, both JSON and CSV files were searched
        foreach (var dir in testDirectories)
        {
            mockService.Verify(
                s => s.GetFiles(dir, "*.json"),
                Times.Once,
                $"Failed to search for JSON files in {dir}");

            mockService.Verify(
                s => s.GetFiles(dir, "*.csv"),
                Times.Once,
                $"Failed to search for CSV files in {dir}");
        }

        // Verify that ReadAllTextAsync was called for both file types in each directory
        mockService.Verify(
            s => s.ReadAllTextAsync(It.IsAny<string>()),
            Times.Exactly(testDirectories.Length * 2));
    }

    [Fact]
    public void ProcessCodeSystemCsv_InvalidColumnCount_ThrowsException()
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        // Create test service with mocked file system methods
        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        mockService
            .Setup(x => x.SetCodeGroup(It.IsAny<CodeGroup>()));

        mockService
            .Setup(x => x.SetCodeGroup(It.IsAny<CodeGroup>()));

        // Arrange
        var codeGroup = new CodeGroup
        {
            Id = "test-cs",
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Url = "http://test.com/cs",
            Version = "1.0"
        };

        var csvContent = @"code,display,status,extra
123,Test Display,Active,Extra Column";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            mockService.Object.ProcessCodeSystemCsv(codeGroup, csv));

        Assert.Contains("CodeSystem CSV must have", ex.Message);
    }

    [Fact]
    public void ProcessValueSetCsv_InvalidColumnCount_ThrowsException()
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        // Create test service with mocked file system methods
        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        // Arrange
        var codeGroup = new CodeGroup
        {
            Id = "test-vs",
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Url = "http://test.com/vs",
            Version = "1.0"
        };

        // Four columns (system,code,display,status) is now valid; five columns is not.
        var csvContent = @"system,code,display,status,extra
http://test.system,123,Test Display,Active,Extra Value";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            mockService.Object.ProcessValueSetCsv(codeGroup, csv));

        Assert.Contains("ValueSet CSV must have", ex.Message);
    }

    [Fact]
    public void ProcessValueSetCsv_WithValidData_CallsSetCodeGroup()
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        // Create test service with mocked file system methods
        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        mockService
            .Setup(x => x.SetCodeGroup(It.IsAny<CodeGroup>()))
            .Verifiable();

        var csvData = "system,code,display\r\n" +
                     "http://test.system,123,Test Display\r\n" +
                     "http://test.system,456,Another Display";

        // Use the same reader configuration LoadCache builds (MissingFieldFound tolerated), since a
        // 3-column value set has no field at the optional status index.
        using var csv = CreateCsvReader(csvData);

        var codeGroup = new CodeGroup
        {
            Id = "test-id",
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Url = "http://test.valueset",
            Version = "1.0",
            Resource = new ValueSet
            {
                Id = "test-id",
                Url = "http://test.valueset",
                Version = "1.0"
            }
        };

        // Act
        mockService.Object.ProcessValueSetCsv(codeGroup, csv);

        // Verify that processing resulted in calling SetGroup with correct CodeGroup
        mockService.Verify(x => x.SetCodeGroup(It.Is<CodeGroup>(cg =>
            cg.Id == "test-id" &&
            cg.Type == CodeGroup.CodeGroupTypes.ValueSet &&
            cg.Url == "http://test.valueset" &&
            cg.Version == "1.0" &&
            cg.Codes.ContainsKey("http://test.system") &&
            cg.Codes["http://test.system"].Count == 2 &&
            cg.Codes["http://test.system"][0].Value == "123" &&
            cg.Codes["http://test.system"][0].Display == "Test Display" &&
            cg.Codes["http://test.system"][1].Value == "456" &&
            cg.Codes["http://test.system"][1].Display == "Another Display")),
            Times.Once);
    }

    [Fact]
    public void ProcessValueSetCsv_WithScientificNotationCodes_LogsSingleAggregatedWarning()
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        mockService
            .Setup(x => x.SetCodeGroup(It.IsAny<CodeGroup>()));

        var csvData = "system,code,display\r\n" +
                      "http://test.system,1e10,One\r\n" +
                      "http://test.system,123,Two\r\n" +
                      "http://test.system,2E+05,Three";

        using var reader = new StringReader(csvData);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var codeGroup = new CodeGroup
        {
            Id = "test-id",
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Url = "http://test.valueset",
            Version = "1.0",
            Resource = new ValueSet
            {
                Id = "test-id",
                Url = "http://test.valueset",
                Version = "1.0"
            }
        };

        mockService.Object.ProcessValueSetCsv(codeGroup, csv);

        VerifyScientificNotationWarning(2, "test-id");
    }

    [Theory]
    [InlineData("code,display\r\n" +
                "123,Test Display\r\n" +
                "456,Another Display")]
    [InlineData("code,display,status\r\n" +
                "123,Test Display,Active\r\n" +
                "456,Another Display,")]
    public void ProcessCodeSystemCsv_WithTwoOrThreeColumnHeader_CallsSetCodeGroup(string csvData)
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        // Create test service with mocked file system methods
        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        mockService
            .Setup(x => x.SetCodeGroup(It.IsAny<CodeGroup>()))
            .Verifiable();

        using var csv = CreateCsvReader(csvData);

        var codeGroup = new CodeGroup
        {
            Id = "test-id",
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Url = "http://test.codesystem",
            Version = "1.0",
            Resource = new CodeSystem
            {
                Id = "test-id",
                Url = "http://test.codesystem",
                Version = "1.0"
            }
        };

        // Act
        mockService.Object.ProcessCodeSystemCsv(codeGroup, csv);

        // Assert - both header shapes yield the same code/display parsing
        mockService.Verify(x => x.SetCodeGroup(It.Is<CodeGroup>(cg =>
            cg.Id == "test-id" &&
            cg.Type == CodeGroup.CodeGroupTypes.CodeSystem &&
            cg.Url == "http://test.codesystem" &&
            cg.Version == "1.0" &&
            cg.Codes.ContainsKey("http://test.codesystem") &&
            cg.Codes["http://test.codesystem"].Count == 2 &&
            cg.Codes["http://test.codesystem"][0].Value == "123" &&
            cg.Codes["http://test.codesystem"][0].Display == "Test Display" &&
            cg.Codes["http://test.codesystem"][1].Value == "456" &&
            cg.Codes["http://test.codesystem"][1].Display == "Another Display")),
            Times.Once);
    }

    [Fact]
    public void ProcessCodeSystemCsv_WithValidData_CallsSetCodeGroup()
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        // Create test service with mocked file system methods
        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        mockService
            .Setup(x => x.SetCodeGroup(It.IsAny<CodeGroup>()))
            .Verifiable();

        var csvData = @"code,display
123,Test Display
456,Another Display";

        using var csv = CreateCsvReader(csvData);

        var codeGroup = new CodeGroup
        {
            Id = "test-id",
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Url = "http://test.codesystem",
            Version = "1.0",
            Resource = new CodeSystem
            {
                Id = "test-id",
                Url = "http://test.codesystem",
                Version = "1.0"
            }
        };

        // Act
        mockService.Object.ProcessCodeSystemCsv(codeGroup, csv);

        // Verify that processing resulted in calling SetGroup with correct CodeGroup
        mockService.Verify(x => x.SetCodeGroup(It.Is<CodeGroup>(cg =>
            cg.Id == "test-id" &&
            cg.Type == CodeGroup.CodeGroupTypes.CodeSystem &&
            cg.Url == "http://test.codesystem" &&
            cg.Version == "1.0" &&
            cg.Codes.ContainsKey("http://test.codesystem") &&
            cg.Codes["http://test.codesystem"].Count == 2 &&
            cg.Codes["http://test.codesystem"][0].Value == "123" &&
            cg.Codes["http://test.codesystem"][0].Display == "Test Display" &&
            cg.Codes["http://test.codesystem"][1].Value == "456" &&
            cg.Codes["http://test.codesystem"][1].Display == "Another Display")),
            Times.Once);
    }

    [Fact]
    public void ProcessCodeSystemCsv_WithScientificNotationCodes_LogsSingleAggregatedWarning()
    {
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        mockConfig.Setup(x => x.Value).Returns(_config);

        var mockService = new Mock<CodeGroupCacheService>(
            _loggerMock.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };

        mockService
            .Setup(x => x.SetCodeGroup(It.IsAny<CodeGroup>()));

        var csvData = "code,display\r\n" +
                      "1e10,One\r\n" +
                      "123,Two\r\n" +
                      "2E+05,Three";

        using var csv = CreateCsvReader(csvData);

        var codeGroup = new CodeGroup
        {
            Id = "test-id",
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Url = "http://test.codesystem",
            Version = "1.0",
            Resource = new CodeSystem
            {
                Id = "test-id",
                Url = "http://test.codesystem",
                Version = "1.0"
            }
        };

        mockService.Object.ProcessCodeSystemCsv(codeGroup, csv);

        VerifyScientificNotationWarning(2, "test-id");
    }

    [Fact]
    public async Task LoadCache_PopulatesCacheWithRetrievableCodeSystem()
    {
        // Use a real memory cache and the real service (only the file-system seams are
        // overridden) so LoadCache/ProcessCodeSystemCsv/SetCodeGroup are all exercised.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/cs"] = new[] { "cs.json", "cs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["cs.json"] = "{ \"resourceType\": \"CodeSystem\", \"id\": \"test-cs\", " +
                          "\"url\": \"http://test.codesystem\", \"version\": \"1.0\" }",
            ["cs.csv"] = "code,display,status\r\n" +
                         "123,Test Display,Active\r\n" +
                         "456,Another Display,Inactive\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - the code group is retrievable from the cache with all its codes.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.CodeSystem, "http://test.codesystem");

        Assert.NotNull(codeGroup);
        Assert.Equal("test-cs", codeGroup.Id);
        Assert.Equal("1.0", codeGroup.Version);
        Assert.True(codeGroup.Codes.ContainsKey("http://test.codesystem"));

        var codes = codeGroup.Codes["http://test.codesystem"];
        Assert.Equal(2, codes.Count);
        Assert.Equal("123", codes[0].Value);
        Assert.Equal("Test Display", codes[0].Display);
        Assert.Equal(CodeStatus.Active, ((CodeSystemCode)codes[0]).Status);
        Assert.Equal("456", codes[1].Value);
        Assert.Equal("Another Display", codes[1].Display);
        Assert.Equal(CodeStatus.Inactive, ((CodeSystemCode)codes[1]).Status);
    }

    [Theory]
    [InlineData("http://test.codesystem")]        // no version
    [InlineData("http://test.codesystem|1.0")]    // exact version suffix
    [InlineData("http://test.codesystem|9.9")]    // unknown version -> falls back to latest loaded
    public async Task GetCodeGroup_ResolvesCanonicalUrlWithVersionSuffix(string lookupUrl)
    {
        // HAPI sends versioned canonical URLs (e.g. ".../identifier-use|4.0.1"); the version
        // suffix must not prevent the URL from resolving to the cached code group.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/cs"] = new[] { "cs.json", "cs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["cs.json"] = "{ \"resourceType\": \"CodeSystem\", \"id\": \"test-cs\", " +
                          "\"url\": \"http://test.codesystem\", \"version\": \"1.0\" }",
            ["cs.csv"] = "code,display,status\r\n123,Test Display,Active\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);
        await service.LoadCache();

        var codeGroup = service.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, lookupUrl);

        Assert.NotNull(codeGroup);
        Assert.Equal("http://test.codesystem", codeGroup.Url);
        Assert.Equal("1.0", codeGroup.Version);
    }

    [Fact]
    public async Task LoadCache_BlankStatus_DefaultsToActive()
    {
        // Use a real memory cache and the real service (only the file-system seams are
        // overridden) so LoadCache/ProcessCodeSystemCsv/SetCodeGroup are all exercised.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/cs"] = new[] { "cs.json", "cs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["cs.json"] = "{ \"resourceType\": \"CodeSystem\", \"id\": \"test-cs\", " +
                          "\"url\": \"http://test.codesystem\", \"version\": \"1.0\" }",
            // Second row has a blank status column, which should default to Active.
            ["cs.csv"] = "code,display,status\r\n" +
                         "123,Test Display,Inactive\r\n" +
                         "456,Another Display,\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - the blank-status row is loaded as Active.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.CodeSystem, "http://test.codesystem");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.codesystem"];
        Assert.Equal(2, codes.Count);
        Assert.Equal("456", codes[1].Value);
        Assert.Equal(CodeStatus.Active, ((CodeSystemCode)codes[1]).Status);
    }

    [Fact]
    public async Task LoadCache_NoStatusColumn_AllRowsDefaultToActive()
    {
        // Use a real memory cache and the real service (only the file-system seams are
        // overridden) so LoadCache/ProcessCodeSystemCsv/SetCodeGroup are all exercised.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/cs"] = new[] { "cs.json", "cs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["cs.json"] = "{ \"resourceType\": \"CodeSystem\", \"id\": \"test-cs\", " +
                          "\"url\": \"http://test.codesystem\", \"version\": \"1.0\" }",
            // No status column at all - every row should default to Active.
            ["cs.csv"] = "code,display\r\n" +
                         "123,Test Display\r\n" +
                         "456,Another Display\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - every code is loaded as Active.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.CodeSystem, "http://test.codesystem");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.codesystem"];
        Assert.Equal(2, codes.Count);
        Assert.All(codes, code => Assert.Equal(CodeStatus.Active, ((CodeSystemCode)code).Status));
    }

    [Fact]
    public async Task LoadCache_MixedCaseStatus_ParsesCaseInsensitively()
    {
        // Use a real memory cache and the real service (only the file-system seams are
        // overridden) so LoadCache/ProcessCodeSystemCsv/SetCodeGroup are all exercised.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/cs"] = new[] { "cs.json", "cs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["cs.json"] = "{ \"resourceType\": \"CodeSystem\", \"id\": \"test-cs\", " +
                          "\"url\": \"http://test.codesystem\", \"version\": \"1.0\" }",
            // Status values in varied casing must all parse and canonicalize to the enum.
            ["cs.csv"] = "code,display,status\r\n" +
                         "123,Test Display,active\r\n" +
                         "456,Another Display,INACTIVE\r\n" +
                         "789,Third Display,Inactive\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - lowercase/uppercase/mixed-case status all load and normalize correctly.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.CodeSystem, "http://test.codesystem");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.codesystem"];
        Assert.Equal(3, codes.Count);
        Assert.Equal(CodeStatus.Active, ((CodeSystemCode)codes[0]).Status);
        Assert.Equal(CodeStatus.Inactive, ((CodeSystemCode)codes[1]).Status);
        Assert.Equal(CodeStatus.Inactive, ((CodeSystemCode)codes[2]).Status);
    }

    [Theory]
    [InlineData("Retired")]  // a plausible-looking status that is not one of the two
    [InlineData("7")]        // numeric: parses as an enum but is not a defined member
    [InlineData("!!")]
    public async Task LoadCache_CodeSystemUnrecognizedStatus_KeepsTheCodeSystemAndDefaultsTheRow(string badStatus)
    {
        // Regression guard. The status column used to be read straight into the enum by CsvHelper, whose
        // converter throws on anything else - and because the records are enumerated lazily that throw
        // escaped ProcessCodeSystemCsv, was swallowed by LoadCache's catch, and cost the WHOLE code system.
        // One malformed cell must not delete thousands of good codes, so the row defaults to Active instead
        // and the rest of the file loads normally.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/cs"] = new[] { "cs.json", "cs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["cs.json"] = "{ \"resourceType\": \"CodeSystem\", \"id\": \"test-cs\", " +
                          "\"url\": \"http://test.codesystem\", \"version\": \"1.0\" }",
            ["cs.csv"] = "code,display,status\r\n" +
                         "123,Test Display,Inactive\r\n" +
                         $"456,Another Display,{badStatus}\r\n" +
                         "789,Third Display,Inactive\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - the code system is still cached, and the rows either side of the bad one kept their status.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.CodeSystem, "http://test.codesystem");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.codesystem"];
        Assert.Equal(3, codes.Count);
        Assert.Equal(CodeStatus.Inactive, ((CodeSystemCode)codes[0]).Status);
        Assert.Equal(CodeStatus.Active, ((CodeSystemCode)codes[1]).Status);
        Assert.Equal(CodeStatus.Inactive, ((CodeSystemCode)codes[2]).Status);

        VerifyUnrecognizedStatusWarning();
    }

    [Fact]
    public async Task LoadCache_ValueSetUnrecognizedStatus_DefaultsTheRowAndWarns()
    {
        // The value set loader already defaulted a bad status to Active, but did so silently, so a typo in
        // the membership column looked exactly like a code that was meant to stay active. Both loaders now
        // default the row AND say what they saw.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/vs"] = new[] { "vs.json", "vs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["vs.json"] = "{ \"resourceType\": \"ValueSet\", \"id\": \"test-vs\", " +
                          "\"url\": \"http://test.valueset\", \"version\": \"1.0\" }",
            ["vs.csv"] = "system,code,display,status\r\n" +
                         "http://test.system,123,Test Display,Inactive\r\n" +
                         "http://test.system,456,Another Display,Retired\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.ValueSet, "http://test.valueset");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.system"];
        Assert.Equal(2, codes.Count);
        Assert.Equal(CodeStatus.Inactive, ((ValueSetCode)codes[0]).Status);
        Assert.Equal(CodeStatus.Active, ((ValueSetCode)codes[1]).Status);

        VerifyUnrecognizedStatusWarning();
    }

    /// <summary>
    /// Asserts the single per-file warning that names the unparseable status values. It is logged once per
    /// code group rather than once per row: these loops run over every code in the file, and a large code
    /// system carries hundreds of thousands of them.
    /// </summary>
    private void VerifyUnrecognizedStatusWarning() =>
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("unrecognized status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

    [Fact]
    public async Task LoadCache_ValueSetWithStatusColumn_LoadsValueSetCodeWithStatus()
    {
        // A four-column value set file (system,code,display,status) carries its own membership
        // status, which is loaded as a ValueSetCode and is authoritative over the code system.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/vs"] = new[] { "vs.json", "vs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["vs.json"] = "{ \"resourceType\": \"ValueSet\", \"id\": \"test-vs\", " +
                          "\"url\": \"http://test.valueset\", \"version\": \"1.0\" }",
            ["vs.csv"] = "system,code,display,status\r\n" +
                         "http://test.system,123,Test Display,Active\r\n" +
                         "http://test.system,456,Another Display,Inactive\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - members are ValueSetCode instances carrying the file's membership status.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.ValueSet, "http://test.valueset");

        Assert.NotNull(codeGroup);
        Assert.Equal("test-vs", codeGroup.Id);
        var codes = codeGroup.Codes["http://test.system"];
        Assert.Equal(2, codes.Count);
        Assert.Equal(CodeStatus.Active, ((ValueSetCode)codes[0]).Status);
        Assert.Equal(CodeStatus.Inactive, ((ValueSetCode)codes[1]).Status);
    }

    [Fact]
    public async Task LoadCache_ValueSetBlankStatus_DefaultsToActive()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/vs"] = new[] { "vs.json", "vs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["vs.json"] = "{ \"resourceType\": \"ValueSet\", \"id\": \"test-vs\", " +
                          "\"url\": \"http://test.valueset\", \"version\": \"1.0\" }",
            // Second row has a blank status column, which should default to Active.
            ["vs.csv"] = "system,code,display,status\r\n" +
                         "http://test.system,123,Test Display,Inactive\r\n" +
                         "http://test.system,456,Another Display,\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - the blank-status member is loaded as an Active ValueSetCode.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.ValueSet, "http://test.valueset");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.system"];
        Assert.Equal(2, codes.Count);
        Assert.Equal("456", codes[1].Value);
        Assert.Equal(CodeStatus.Active, ((ValueSetCode)codes[1]).Status);
    }

    [Fact]
    public async Task LoadCache_ValueSetNoStatusColumn_LoadsPlainCode()
    {
        // A three-column value set file has no membership status, so its members are plain Code
        // instances (not ValueSetCode) and fall back to the code system status when validated.
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/vs"] = new[] { "vs.json", "vs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["vs.json"] = "{ \"resourceType\": \"ValueSet\", \"id\": \"test-vs\", " +
                          "\"url\": \"http://test.valueset\", \"version\": \"1.0\" }",
            ["vs.csv"] = "system,code,display\r\n" +
                         "http://test.system,123,Test Display\r\n" +
                         "http://test.system,456,Another Display\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - members are plain Code (no membership status), not ValueSetCode.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.ValueSet, "http://test.valueset");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.system"];
        Assert.Equal(2, codes.Count);
        Assert.All(codes, code => Assert.Equal(
            typeof(LantanaGroup.Link.Terminology.Application.Models.Code), code.GetType()));
    }

    [Fact]
    public async Task LoadCache_ValueSetMixedCaseStatus_ParsesCaseInsensitively()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/vs"] = new[] { "vs.json", "vs.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["vs.json"] = "{ \"resourceType\": \"ValueSet\", \"id\": \"test-vs\", " +
                          "\"url\": \"http://test.valueset\", \"version\": \"1.0\" }",
            ["vs.csv"] = "system,code,display,status\r\n" +
                         "http://test.system,123,Test Display,active\r\n" +
                         "http://test.system,456,Another Display,INACTIVE\r\n" +
                         "http://test.system,789,Third Display,Inactive\r\n"
        };

        var service = new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);

        // Act
        await service.LoadCache();

        // Assert - lowercase/uppercase/mixed-case status all load and normalize correctly.
        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.ValueSet, "http://test.valueset");

        Assert.NotNull(codeGroup);
        var codes = codeGroup.Codes["http://test.system"];
        Assert.Equal(3, codes.Count);
        Assert.Equal(CodeStatus.Active, ((ValueSetCode)codes[0]).Status);
        Assert.Equal(CodeStatus.Inactive, ((ValueSetCode)codes[1]).Status);
        Assert.Equal(CodeStatus.Inactive, ((ValueSetCode)codes[2]).Status);
    }

    // Loads two versions of the same code group into a real cache. Defaults to "4.0.9" and
    // "4.0.10", where string ordering would wrongly rank "4.0.9" above "4.0.10" but semantic
    // ordering ranks "4.0.10" as the latest. Callers can supply other version strings (including
    // null, blank, or non-numeric) to exercise the CompareVersions fallback path. Used by the
    // "latest version" resolution tests below.
    private TestableCodeGroupCacheService BuildTwoVersionService(
        IMemoryCache memoryCache, string? versionA = "4.0.9", string? versionB = "4.0.10")
    {
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(_config);

        var directoryFiles = new Dictionary<string, string[]>
        {
            ["/test/path/v9"] = new[] { "v9.json", "v9.csv" },
            ["/test/path/v10"] = new[] { "v10.json", "v10.csv" }
        };
        var fileContents = new Dictionary<string, string>
        {
            ["v9.json"] = BuildCodeSystemJson(versionA),
            ["v9.csv"] = "code,display,status\r\n123,Test Display,Active\r\n",
            ["v10.json"] = BuildCodeSystemJson(versionB),
            ["v10.csv"] = "code,display,status\r\n123,Test Display,Active\r\n"
        };

        return new TestableCodeGroupCacheService(
            _loggerMock.Object, memoryCache, mockConfig.Object, directoryFiles, fileContents);
    }

    // Builds a minimal CodeSystem document with the given version. A null version omits the
    // "version" field entirely (FHIR version is optional), yielding a null CodeGroup.Version.
    private static string BuildCodeSystemJson(string? version)
    {
        var versionField = version is null ? "" : $", \"version\": \"{version}\"";
        return "{ \"resourceType\": \"CodeSystem\", \"id\": \"test-cs\", " +
               "\"url\": \"http://test.codesystem\"" + versionField + " }";
    }

    private void VerifyScientificNotationWarning(int expectedCount, string expectedCodeGroupId)
    {
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(level => level == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains($"Found {expectedCount} code(s)") &&
                    state.ToString()!.Contains($"code group {expectedCodeGroupId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCodeGroup_NoVersion_ReturnsSemanticallyLatestVersion()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildTwoVersionService(memoryCache);
        await service.LoadCache();

        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.CodeSystem, "http://test.codesystem");

        Assert.NotNull(codeGroup);
        // String ordering would return "4.0.9"; semantic ordering returns "4.0.10".
        Assert.Equal("4.0.10", codeGroup.Version);
    }

    [Fact]
    public async Task GetCodeGroupById_NoVersion_ReturnsSemanticallyLatestVersion()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildTwoVersionService(memoryCache);
        await service.LoadCache();

        var codeGroup = service.GetCodeGroupById(
            CodeGroup.CodeGroupTypes.CodeSystem, "test-cs");

        Assert.NotNull(codeGroup);
        Assert.Equal("4.0.10", codeGroup.Version);
    }

    [Fact]
    public async Task GetAllCodeGroups_ReturnsSemanticallyLatestVersionPerId()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildTwoVersionService(memoryCache);
        await service.LoadCache();

        var codeGroups = service.GetAllCodeGroups(CodeGroup.CodeGroupTypes.CodeSystem);

        var codeGroup = Assert.Single(codeGroups);
        Assert.Equal("4.0.10", codeGroup.Version);
    }

    // When one or both versions fail TryParseVersion (null, blank, or non-numeric), CompareVersions
    // falls back to a case-insensitive ordinal string comparison, so OrderByDescending resolves the
    // "latest" as the version that sorts last under that ordering.
    [Theory]
    [InlineData(null, "2.0", "2.0")]            // null fails parse; null sorts before "2.0"
    [InlineData("", "2.0", "2.0")]              // blank fails parse; "" sorts before "2.0"
    [InlineData("2.0", "unknown", "unknown")]   // numeric vs non-numeric; 'u' (0x75) > '2' (0x32)
    [InlineData("beta", "unknown", "unknown")]  // both non-numeric; 'u' > 'b'
    [InlineData("alpha", "Beta", "Beta")]       // case-insensitive: "beta" > "alpha" though 'B' < 'a' ordinally
    public async Task GetCodeGroup_UnparseableVersions_FallBackToCaseInsensitiveOrdinal(
        string? versionA, string? versionB, string expectedLatest)
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildTwoVersionService(memoryCache, versionA, versionB);
        await service.LoadCache();

        var codeGroup = service.GetCodeGroup(
            CodeGroup.CodeGroupTypes.CodeSystem, "http://test.codesystem");

        Assert.NotNull(codeGroup);
        Assert.Equal(expectedLatest, codeGroup.Version);
    }

    /// <summary>
    /// Real <see cref="CodeGroupCacheService"/> with only the file-system seams overridden,
    /// so LoadCache and everything it calls run against the actual implementation.
    /// </summary>
    private sealed class TestableCodeGroupCacheService : CodeGroupCacheService
    {
        private readonly IReadOnlyDictionary<string, string[]> _directoryFiles;
        private readonly IReadOnlyDictionary<string, string> _fileContents;

        public TestableCodeGroupCacheService(
            ILogger<CodeGroupCacheService> logger,
            IMemoryCache cache,
            IOptions<TerminologyConfig> config,
            IReadOnlyDictionary<string, string[]> directoryFiles,
            IReadOnlyDictionary<string, string> fileContents)
            : base(logger, cache, config)
        {
            _directoryFiles = directoryFiles;
            _fileContents = fileContents;
        }

        protected internal override bool DirectoryExists(string path) => true;

        protected internal override string[] GetDirectories(string path) =>
            _directoryFiles.Keys.ToArray();

        protected internal override string[] GetFiles(string path, string searchPattern)
        {
            var extension = searchPattern.TrimStart('*');
            return _directoryFiles.TryGetValue(path, out var files)
                ? files.Where(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase)).ToArray()
                : Array.Empty<string>();
        }

        protected internal override Task<string> ReadAllTextAsync(string path) =>
            Task.FromResult(_fileContents[path]);
    }
}
