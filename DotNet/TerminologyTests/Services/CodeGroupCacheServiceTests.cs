using System.Globalization;
using CsvHelper;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Terminology.Tests.Services;

public class CodeGroupCacheServiceTests
{
    private readonly Mock<ILogger<CodeGroupCacheService>> _loggerMock;
    private readonly TerminologyConfig _config;

    public CodeGroupCacheServiceTests()
    {
        _loggerMock = new Mock<ILogger<CodeGroupCacheService>>();
        _config = new TerminologyConfig { Path = "/test/path" };
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
        
        // Arrange
        var codeGroup = new CodeGroup
        {
            Id = "test-cs",
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Url = "http://test.com/cs",
            Version = "1.0"
        };

        var csvContent = @"code,display,extra
123,Test Display,Extra Column";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => 
            mockService.Object.ProcessCodeSystemCsv(codeGroup, csv));

        Assert.Contains("CodeSystem CSV must have exactly 2 columns", ex.Message);
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

        var csvContent = @"system,code,display,extra
http://test.system,123,Test Display,Extra Value";

        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            mockService.Object.ProcessValueSetCsv(codeGroup, csv));

        Assert.Contains("ValueSet CSV must have exactly 3 columns", ex.Message);
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

        using var reader = new StringReader(csvData);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

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
}