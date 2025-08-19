using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LantanaGroup.Link.Terminology.Tests.Services;

public class CodeGroupCacheServiceTests
{
    [Fact]
    public async Task LoadCache_ShouldAttemptToLoadFilesFromEachDirectory()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGroupCacheService>>();
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();

        var config = new TerminologyConfig
        {
            Path = "/test/path"
        };
        mockConfig.Setup(x => x.Value).Returns(config);

        // Mock directories to return
        var testDirectories = new[]
        {
            "/test/path/dir1",
            "/test/path/dir2",
            "/test/path/dir3"
        };

        // Create test service with mocked file system methods
        var mockService = new Mock<CodeGroupCacheService>(
            mockLogger.Object,
            mockCache.Object,
            mockConfig.Object)
        {
            CallBase = true
        };
        
        mockService
            .Setup(s => s.DirectoryExists(It.IsAny<string>()))
            .Returns(true);

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
            .ReturnsAsync("code,display,system\r\n" +
                          "abcd,Some Code,http://somesystem.com\r\n");

        // Act
        mockService.Object.LoadCache();

        mockService.Verify(
            s => s.DirectoryExists(config.Path),
            Times.Once);

        // Assert
        // Verify that GetDirectories was called once with the config path
        mockService.Verify(
            s => s.GetDirectories(config.Path),
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
}