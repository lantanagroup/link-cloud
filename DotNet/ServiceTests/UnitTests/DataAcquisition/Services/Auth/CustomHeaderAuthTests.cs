using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Auth;

[Trait("Category", "UnitTests")]
public class CustomHeaderAuthTests
{
    private readonly Mock<ISecretManager> _mockSecretManager;

    public CustomHeaderAuthTests()
    {
        _mockSecretManager = new Mock<ISecretManager>();
        _mockSecretManager
            .Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => name);
    }

    private CustomHeaderAuth BuildSut() => new(_mockSecretManager.Object);

    [Fact]
    public async Task SetAuthentication_WithNullCustomHeaders_ReturnsEmptyDictionary()
    {
        // Arrange
        var customHeaderAuth = BuildSut();
        var facilityId = "test-facility-id";
        var authSettings = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = null
        };

        // Act
        var result = await customHeaderAuth.SetAuthentication(facilityId, authSettings);

        // Assert
        Assert.False(result.isQueryParam);
        var returnedHeaders = Assert.IsType<Dictionary<string, string>>(result.authHeaderValue);
        Assert.Empty(returnedHeaders);
    }

    [Fact]
    public async System.Threading.Tasks.Task SetAuthentication_WithEmptyCustomHeaders_ReturnsEmptyDictionary()
    {
        // Arrange
        var customHeaderAuth = BuildSut();
        var facilityId = "test-facility-id";
        var authSettings = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = new Dictionary<string, string>()
        };

        // Act
        var result = await customHeaderAuth.SetAuthentication(facilityId, authSettings);

        // Assert
        Assert.False(result.isQueryParam);
        var returnedHeaders = Assert.IsType<Dictionary<string, string>>(result.authHeaderValue);
        Assert.Empty(returnedHeaders);
    }

    [Fact]
    public async Task SetAuthentication_WithSingleCustomHeader_ReturnsHeaderDictionary()
    {
        // Arrange
        var customHeaderAuth = BuildSut();
        var facilityId = "test-facility-id";
        var expectedHeaders = new Dictionary<string, string>
        {
            { "X-Custom-Header", "CustomValue" }
        };
        var authSettings = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = expectedHeaders
        };

        // Act
        var result = await customHeaderAuth.SetAuthentication(facilityId, authSettings);

        // Assert
        Assert.False(result.isQueryParam);
        Assert.NotNull(result.authHeaderValue);
        Assert.IsType<Dictionary<string, string>>(result.authHeaderValue);
        var returnedHeaders = (Dictionary<string, string>)result.authHeaderValue;
        Assert.Single(returnedHeaders);
        Assert.Equal("CustomValue", returnedHeaders["X-Custom-Header"]);
    }

    [Fact]
    public async Task SetAuthentication_WithMultipleCustomHeaders_ReturnsAllHeaders()
    {
        // Arrange
        var customHeaderAuth = BuildSut();
        var facilityId = "test-facility-id";
        var expectedHeaders = new Dictionary<string, string>
        {
            { "X-API-Key", "api-key-value" },
            { "X-Tenant-Id", "tenant-123" },
            { "X-Client-Version", "1.0.0" }
        };
        var authSettings = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = expectedHeaders
        };

        // Act
        var result = await customHeaderAuth.SetAuthentication(facilityId, authSettings);

        // Assert
        Assert.False(result.isQueryParam);
        Assert.NotNull(result.authHeaderValue);
        Assert.IsType<Dictionary<string, string>>(result.authHeaderValue);
        var returnedHeaders = (Dictionary<string, string>)result.authHeaderValue;
        Assert.Equal(3, returnedHeaders.Count);
        Assert.Equal("api-key-value", returnedHeaders["X-API-Key"]);
        Assert.Equal("tenant-123", returnedHeaders["X-Tenant-Id"]);
        Assert.Equal("1.0.0", returnedHeaders["X-Client-Version"]);
    }

    [Fact]
    public async Task SetAuthentication_IsQueryParamAlwaysFalse()
    {
        // Arrange
        var customHeaderAuth = BuildSut();
        var facilityId = "test-facility-id";
        var authSettings = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = new Dictionary<string, string>
            {
                { "X-Test", "test-value" }
            }
        };

        // Act
        var result = await customHeaderAuth.SetAuthentication(facilityId, authSettings);

        // Assert
        Assert.False(result.isQueryParam);
    }

    [Fact]
    public async Task SetAuthentication_ReturnsEqualHeadersObject()
    {
        // Arrange
        var customHeaderAuth = BuildSut();
        var facilityId = "test-facility-id";
        var expectedHeaders = new Dictionary<string, string>
        {
            { "X-Custom", "Value" }
        };
        var authSettings = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = expectedHeaders
        };

        // Act
        var result = await customHeaderAuth.SetAuthentication(facilityId, authSettings);

        // Assert
        Assert.Equal(expectedHeaders, result.authHeaderValue);
    }

    [Fact]
    public async Task SetAuthentication_WithDifferentFacilityIds_ReturnsExpectedHeaders()
    {
        // Arrange
        var customHeaderAuth = BuildSut();
        var headers1 = new Dictionary<string, string> { { "X-Header-1", "Value-1" } };
        var headers2 = new Dictionary<string, string> { { "X-Header-2", "Value-2" } };
        
        var authSettings1 = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = headers1
        };
        
        var authSettings2 = new AuthenticationConfigurationModel
        {
            AuthType = "CustomHeaders",
            CustomHeaders = headers2
        };

        // Act
        var result1 = await customHeaderAuth.SetAuthentication("facility-1", authSettings1);
        var result2 = await customHeaderAuth.SetAuthentication("facility-2", authSettings2);

        // Assert
        Assert.Equal(headers1, result1.authHeaderValue);
        Assert.Equal(headers2, result2.authHeaderValue);
    }
}
