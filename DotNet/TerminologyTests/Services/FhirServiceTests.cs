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

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(new CodeGroup());

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, null, code, null, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        var messageParameter = result.GetSingleValue<FhirString>("message");

        Assert.NotNull(resultParameter);
        Assert.False(resultParameter.Value);
        Assert.Equal("No valid code found in parameters", messageParameter?.Value);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithCodeAndSystem_InParametersParts_ReturnsTrue()
    {
        // Arrange
        var valueSetId = "test-vs-params";
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

        // Build Parameters with code and system
        var parameters = new Parameters();
        parameters.Add("code", new FhirString(code));
        parameters.Add("system", new FhirUri(system));
        parameters.Add("display", new FhirString(display));

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, null, null, null, parameters);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithCoding_InParameters_ReturnsTrue()
    {
        // Arrange
        var valueSetId = "test-vs-coding";
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

        // Build Parameters with Coding
        var parameters = new Parameters();
        parameters.Add("coding", new Coding(system, code, display));

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, null, null, null, parameters);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithCodeableConcept_InParameters_ReturnsTrue()
    {
        // Arrange
        var valueSetId = "test-vs-codeableconcept";
        var correctCode = "good-code";
        var wrongCode = "bad-code";
        var system = "http://test.system";
        var display = "Good Code";

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
                        new() { Value = correctCode, Display = display }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Build Parameters with CodeableConcept containing multiple codings
        var concept = new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new(system, wrongCode, "Wrong Display"),
                new(system, correctCode, display)
            }
        };
        var parameters = new Parameters();
        parameters.Add("codeableConcept", concept);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, null, null, null, parameters);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
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

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemId, It.IsAny<string>()))
            .Returns(new CodeGroup());

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, code, null, null);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        var messageParameter = result.GetSingleValue<FhirString>("message");

        Assert.NotNull(resultParameter);
        Assert.False(resultParameter.Value);
        Assert.Equal("No valid code found in parameters", messageParameter?.Value);
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

    [Fact]
    public void ValidateCodeInCodeSystem_WithCode_InParametersParts_ReturnsTrue()
    {
        // Arrange
        var codeSystemId = "test-cs-params";
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

        // Build Parameters with code (and display)
        var parameters = new Parameters();
        parameters.Add("code", new FhirString(code));
        parameters.Add("display", new FhirString(display));

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, null, null, parameters);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithCoding_InParameters_ReturnsTrue()
    {
        // Arrange
        var codeSystemId = "test-cs-coding";
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

        // Build Parameters with Coding
        var parameters = new Parameters();
        parameters.Add("coding", new Coding(system, code, display));

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, null, null, parameters);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithCodeableConcept_InParameters_ReturnsTrue()
    {
        // Arrange
        var codeSystemId = "test-cs-codeableconcept";
        var correctCode = "good-code";
        var wrongCode = "bad-code";
        var system = "http://test.system";
        var display = "Good Code";

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
                        new() { Value = correctCode, Display = display }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Build Parameters with CodeableConcept containing multiple codings
        var concept = new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new(system, wrongCode, "Wrong Display"),
                new(system, correctCode, display)
            }
        };
        var parameters = new Parameters();
        parameters.Add("codeableConcept", concept);

        // Act
        var result = _service.ValidateCodeInCodeSystem(null, codeSystemId, null, null, parameters);

        // Assert
        Assert.NotNull(result);
        var resultParameter = result.GetSingleValue<FhirBoolean>("result");
        Assert.NotNull(resultParameter);
        Assert.True(resultParameter.Value);
    }

    #endregion
}
