using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
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
    private readonly List<ValueSet> _mockValueSets;

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
        
        string valueSet1 = @"
{
  ""resourceType"" : ""ValueSet"",
  ""id"" : ""v3-ActEncounterCode"",
  ""language"" : ""en"",
  ""text"" : {
    ""status"" : ""extensions"",
    ""div"" : ""<div xmlns=\""http://www.w3.org/1999/xhtml\"" xml:lang=\""en\"" lang=\""en\""><p class=\""res-header-id\""><b>Generated Narrative: ValueSet v3-ActEncounterCode</b></p><a name=\""v3-ActEncounterCode\""> </a><a name=\""hcv3-ActEncounterCode\""> </a><a name=\""v3-ActEncounterCode-en-US\""> </a><div style=\""display: inline-block; background-color: #d9e0e7; padding: 6px; margin: 4px; border: 1px solid #8da1b4; border-radius: 5px; line-height: 60%\""><p style=\""margin-bottom: 0px\"">Language: en</p></div><p>This value set includes codes based on the following rules:</p><ul><li>Include codes from<a href=\""CodeSystem-v3-ActCode.html\""><code>http://terminology.hl7.org/CodeSystem/v3-ActCode</code></a> where concept  is-a <a href=\""CodeSystem-v3-ActCode.html#v3-ActCode-_ActEncounterCode\"">_ActEncounterCode</a></li></ul><p>This value set excludes codes based on the following rules:</p><ul><li>Exclude these codes as defined in <a href=\""CodeSystem-v3-ActCode.html\""><code>http://terminology.hl7.org/CodeSystem/v3-ActCode</code></a><table class=\""none\""><tr><td style=\""white-space:nowrap\""><b>Code</b></td><td><b>Display</b></td><td><b>Definition</b></td></tr><tr><td><a href=\""CodeSystem-v3-ActCode.html#v3-ActCode-_ActEncounterCode\"">_ActEncounterCode</a></td><td style=\""color: #cccccc\"">ActEncounterCode</td><td>Domain provides codes that qualify the ActEncounterClass (ENC)</td></tr></table></li></ul></div>""
  },
  ""url"" : ""http://terminology.hl7.org/ValueSet/v3-ActEncounterCode"",
  ""identifier"" : [
    {
      ""system"" : ""urn:ietf:rfc:3986"",
      ""value"" : ""urn:oid:2.16.840.1.113883.1.11.13955""
    }
  ],
  ""version"" : ""3.0.0"",
  ""name"" : ""ActEncounterCode"",
  ""title"" : ""ActEncounterCode"",
  ""status"" : ""active"",
  ""experimental"" : false,
  ""date"" : ""2014-03-26"",
  ""publisher"" : ""Health Level Seven International"",
  ""contact"" : [
    {
      ""telecom"" : [
        {
          ""system"" : ""url"",
          ""value"" : ""http://hl7.org""
        },
        {
          ""system"" : ""email"",
          ""value"" : ""hq@HL7.org""
        }
      ]
    }
  ],
  ""description"" : ""Domain provides codes that qualify the ActEncounterClass (ENC)"",
  ""copyright"" : ""This material derives from the HL7 Terminology THO. THO is copyright ©1989+ Health Level Seven International and is made available under the CC0 designation. For more licensing information see: https://terminology.hl7.org/license.html"",
  ""compose"" : {
    ""include"" : [
      {
        ""system"" : ""http://terminology.hl7.org/CodeSystem/v3-ActCode"",
        ""filter"" : [
          {
            ""property"" : ""concept"",
            ""op"" : ""is-a"",
            ""value"" : ""_ActEncounterCode""
          }
        ]
      }
    ],
    ""exclude"" : [
      {
        ""system"" : ""http://terminology.hl7.org/CodeSystem/v3-ActCode"",
        ""concept"" : [
          {
            ""code"" : ""_ActEncounterCode""
          }
        ]
      }
    ]
  }
}
";

    string valueSet2 = @"
{
    ""resourceType"": ""ValueSet"",
    ""id"": ""v2-0916"",
    ""text"": {
        ""status"": ""generated"",
        ""div"": ""<div xmlns=\""http://www.w3.org/1999/xhtml\""><p class=\""res-header-id\""><b>Generated Narrative: ValueSet v2-0916</b></p><a name=\""v2-0916\""> </a><a name=\""hcv2-0916\""> </a><a name=\""v2-0916-en-US\""> </a><ul><li>Include all codes defined in <a href=\""CodeSystem-v2-0916.html\""><code>http://terminology.hl7.org/CodeSystem/v2-0916</code></a></li></ul></div>""
    },
    ""url"": ""http://terminology.hl7.org/ValueSet/v2-0916"",
    ""identifier"": [
        {
            ""system"": ""urn:ietf:rfc:3986"",
            ""value"": ""urn:oid:2.16.840.1.113883.21.440""
        }
    ],
    ""version"": ""2.0.0"",
    ""name"": ""Hl7VSRelevantClincialInformation"",
    ""title"": ""hl7VS-relevantClincialInformation"",
    ""status"": ""active"",
    ""experimental"": false,
    ""date"": ""2019-12-01"",
    ""publisher"": ""Health Level Seven International"",
    ""contact"": [
        {
            ""telecom"": [
                {
                    ""system"": ""url"",
                    ""value"": ""http://hl7.org""
                },
                {
                    ""system"": ""email"",
                    ""value"": ""hq@HL7.org""
                }
            ]
        }
    ],
    ""description"": ""Value Set of codes that specify additional clinical information about the patient or specimen to report the supporting and/or suspected diagnosis and clinical findings on requests for interpreted diagnostic studies."",
    ""copyright"": ""This material derives from the HL7 Terminology (THO). THO is copyright ©1989+ Health Level Seven International and is made available under the CC0 designation. For more licensing information see: https://terminology.hl7.org/license.html"",
    ""compose"": {
        ""include"": [
            {
                ""system"": ""http://terminology.hl7.org/CodeSystem/v2-0916"",
                ""version"": ""2.0.0""
            }
        ]
    }
}
";
    
        _mockValueSets =
        [
            new FhirJsonParser().Parse<ValueSet>(valueSet1),
            new FhirJsonParser().Parse<ValueSet>(valueSet2)
        ];
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
    
    #region GetValueSets Tests

    [Fact]
    public void GetValueSets_ReturnsListOfValueSets()
    {
        List<CodeGroup> mockCodeGroups =
        [
            new CodeGroup()
            {
                Type = CodeGroup.CodeGroupTypes.ValueSet,
                Id = _mockValueSets[0].Id,
                Identifiers = _mockValueSets[0].Identifier,
                Name = _mockValueSets[0].Name,
                Version = _mockValueSets[0].Version,
                Resource = _mockValueSets[0]
            },
            new CodeGroup()
            {
                Type = CodeGroup.CodeGroupTypes.ValueSet,
                Id = _mockValueSets[1].Id,
                Identifiers = _mockValueSets[1].Identifier,
                Name = _mockValueSets[1].Name,
                Version = _mockValueSets[1].Version,
                Resource = _mockValueSets[1]
            }
        ];

        _mockCacheService
            .Setup(x => x.GetAllCodeGroups(CodeGroup.CodeGroupTypes.ValueSet))
            .Returns(mockCodeGroups);

        // Act
        var result = _service.GetValueSets(null, SummaryType.True);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Entry.Count);
        Assert.Equal("ValueSet", result.Entry[0].Resource.TypeName);
        Assert.Equal(_mockValueSets[0].Id, result.Entry[0].Resource.Id);
        Assert.Equal("ValueSet", result.Entry[1].Resource.TypeName);
        Assert.Equal(_mockValueSets[1].Id, result.Entry[1].Resource.Id);
    }
    
    #endregion
}
