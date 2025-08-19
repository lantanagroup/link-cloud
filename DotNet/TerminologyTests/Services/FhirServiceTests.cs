using System.Collections.Concurrent;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;

namespace LantanaGroup.Link.Terminology.Tests.Services;

public class FhirServiceTests
{
    private readonly Mock<CodeGroupCacheService> _mockCacheService;
    private readonly Mock<ILogger<FhirService>> _mockLogger;
    private readonly FhirService _service;

    public FhirServiceTests()
    {
        Mock<ILogger<CodeGroupCacheService>> mockCacheLogger = new Mock<ILogger<CodeGroupCacheService>>()
        {
            CallBase = true
        };
        TerminologyConfig config = new TerminologyConfig()
        {
            Path = "/test/path"
        };
        Mock<IMemoryCache> mockCache = new Mock<IMemoryCache>();
        Mock<IOptions<TerminologyConfig>> mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(config);
        
        _mockCacheService = new Mock<CodeGroupCacheService>(mockCacheLogger.Object, mockCache.Object, mockConfig.Object);
        _mockLogger = new Mock<ILogger<FhirService>>();
        _service = new FhirService(_mockCacheService.Object, _mockLogger.Object);
    }

    #region ValidateCodeInValueSet Tests

    [Fact]
    public void ValidateCodeInValueSet_WithValidCode_ReturnsTrue()
    {
        // Arrange
        var valueSetId = "test-vs-1";
        var code = "test-code";
        var system = "http://test.system";
        var display = "Test Code";

        var mockCodeGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    system,
                    new List<Code>
                    {
                        new() { Value = code, Display = display }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithInvalidCode_ReturnsFalse()
    {
        // Arrange
        var valueSetId = "test-vs-1";
        var code = "invalid-code";
        var system = "http://test.system";

        var mockCodeGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    system,
                    new List<Code>
                    {
                        new() { Value = "valid-code", Display = "Valid Code" }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, null, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.False(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithMissingCode_ReturnsFalse()
    {
        // Arrange
        var valueSetId = "test-vs-1";
        string? code = null;

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, null, code, null, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        var messageParameter = result.GetSingleValue<FhirString>("message");

        Assert.NotNull(resultParameter);
        Assert.False(resultParameter.Value);
        Assert.Equal("code parameter is required", messageParameter?.Value);
    }

    #endregion

    #region ValidateCodeInCodeSystem Tests

    [Fact]
    public void ValidateCodeInCodeSystem_WithValidCode_ReturnsTrue()
    {
        // Arrange
        var codeSystemId = "test-cs-1";
        var code = "test-code";
        var system = "http://test.system";
        var display = "Test Code";

        var mockCodeGroup = new CodeGroup
        {
            Id = codeSystemId,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    system,
                    new List<Code>
                    {
                        new() { Value = code, Display = display }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, code, display, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithInvalidCode_ReturnsFalse()
    {
        // Arrange
        var codeSystemId = "test-cs-1";
        var code = "invalid-code";
        var system = "http://test.system";

        var mockCodeGroup = new CodeGroup
        {
            Id = codeSystemId,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    system,
                    new List<Code>
                    {
                        new() { Value = "valid-code", Display = "Valid Code" }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, code, null, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.False(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithMissingCode_ReturnsFalse()
    {
        // Arrange
        var codeSystemId = "test-cs-1";
        string? code = null;

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, code, null, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        var messageParameter = result.GetSingleValue<FhirString>("message");

        Assert.NotNull(resultParameter);
        Assert.False(resultParameter.Value);
        Assert.Equal("code parameter is required", messageParameter?.Value);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithValidCodeButMismatchedDisplay_ReturnsFalse()
    {
        // Arrange
        var codeSystemId = "test-cs-1";
        var code = "test-code";
        var system = "http://test.system";
        var display = "Wrong Display";

        var mockCodeGroup = new CodeGroup
        {
            Id = codeSystemId,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    system,
                    new List<Code>
                    {
                        new() { Value = code, Display = "Correct Display" }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, code, display, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        var messageParameter = result.GetSingleValue<FhirString>("message");

        Assert.NotNull(resultParameter);
        Assert.False(resultParameter.Value);
        Assert.Equal("Display does not match code", messageParameter?.Value);
    }

    #endregion
}
