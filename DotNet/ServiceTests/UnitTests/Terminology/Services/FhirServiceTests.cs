using Hl7.Fhir.Rest;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;

namespace UnitTests.Terminology;

public class FhirServiceTests
{
    private readonly Mock<ICodeGroupCacheService> _mockCacheService;
    private readonly Mock<ILogger<FhirService>> _mockLogger;
    private readonly FhirService _service;
    private readonly List<ValueSet> _mockValueSets;

    public FhirServiceTests()
    {
        _mockCacheService = new Mock<ICodeGroupCacheService>();
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
            LinkFhirSerializerOptions.FhirJsonParserPermissive.Parse<ValueSet>(valueSet1),
            LinkFhirSerializerOptions.FhirJsonParserPermissive.Parse<ValueSet>(valueSet2)
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
    public void ValidateCodeInValueSet_WithInactiveMemberCode_ReturnsInactiveIssueOutcome()
    {
        // Arrange
        var valueSetId = "test-vs-inactive-member";
        var code = "inactive-code";
        var system = "http://test.system";
        var display = "Inactive Code";

        // ValueSet members are plain Codes with no status; the CodeSystem carries the inactive status.
        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new() { Value = code, Display = display } } }
            }
        };
        var codeSystemGroup = new CodeGroup
        {
            Url = system,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Inactive } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system, It.IsAny<string>()))
            .Returns(codeSystemGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        var issuesParameter = result.Parameter.FirstOrDefault(p => p.Name == "issues");
        Assert.NotNull(issuesParameter);
        var outcome = Assert.IsType<OperationOutcome>(issuesParameter.Resource);
        var issue = Assert.Single(outcome.Issue);
        Assert.Equal(OperationOutcome.IssueSeverity.Warning, issue.Severity);
        Assert.Equal(OperationOutcome.IssueType.BusinessRule, issue.Code);
        Assert.Equal("Code is inactive.", issue.Details?.Text);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithDuplicateCodeSystemEntries_RejoinsLastStatus()
    {
        // Arrange
        // The CodeSystem lists the member code twice with differing status (Active then Inactive).
        // The value-set rejoin must honor last-one-wins (LEGLINK-814) and report the code inactive.
        var valueSetId = "test-vs-dup-rejoin";
        var code = "ACCTRECEIVABLE";
        var system = "http://test.system";
        var display = "account receivable";

        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new() { Value = code, Display = display } } }
            }
        };
        var codeSystemGroup = new CodeGroup
        {
            Url = system,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    system,
                    new List<Code>
                    {
                        new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Active },
                        new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Inactive }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system, It.IsAny<string>()))
            .Returns(codeSystemGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        var outcome = Assert.IsType<OperationOutcome>(result.Parameter.Single(p => p.Name == "issues").Resource);
        Assert.Equal("Code is inactive.", Assert.Single(outcome.Issue).Details?.Text);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithActiveMemberCode_ReturnsNoIssue()
    {
        // Arrange
        var valueSetId = "test-vs-active-member";
        var code = "active-code";
        var system = "http://test.system";
        var display = "Active Code";

        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new() { Value = code, Display = display } } }
            }
        };
        var codeSystemGroup = new CodeGroup
        {
            Url = system,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Active } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system, It.IsAny<string>()))
            .Returns(codeSystemGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        Assert.Null(result.Parameter.FirstOrDefault(p => p.Name == "issues"));
    }

    [Fact]
    public void ValidateCodeInValueSet_WithCodeSystemNotLoaded_ReturnsActiveNoIssue()
    {
        // Arrange
        var valueSetId = "test-vs-no-cs";
        var code = "some-code";
        var system = "http://unloaded.system";
        var display = "Some Code";

        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new() { Value = code, Display = display } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        // GetCodeGroup(CodeSystem, ...) is left unstubbed -> Moq returns null (CodeSystem not loaded).

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        Assert.Null(result.Parameter.FirstOrDefault(p => p.Name == "issues"));
    }

    [Fact]
    public void ValidateCodeInValueSet_MultiSystem_InactiveInMatchedSystem_ReturnsInactiveIssue()
    {
        // Arrange
        var valueSetId = "test-vs-multi";
        var code = "shared-code";
        var systemA = "http://system.a";
        var systemB = "http://system.b";
        var display = "Shared Code";

        // The code only exists under systemB; passing system=null forces the across-systems path,
        // which must thread the matched systemKey (systemB) into the CodeSystem rejoin.
        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { systemA, new List<Code> { new() { Value = "other", Display = "Other" } } },
                { systemB, new List<Code> { new() { Value = code, Display = display } } }
            }
        };
        var codeSystemB = new CodeGroup
        {
            Url = systemB,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { systemB, new List<Code> { new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Inactive } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, systemB, It.IsAny<string>()))
            .Returns(codeSystemB);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, null, code, display, null);

        // Assert
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        var issuesParameter = result.Parameter.FirstOrDefault(p => p.Name == "issues");
        Assert.NotNull(issuesParameter);
        var outcome = Assert.IsType<OperationOutcome>(issuesParameter.Resource);
        Assert.Equal("Code is inactive.", Assert.Single(outcome.Issue).Details?.Text);
    }

    [Fact]
    public void ValidateCodeInValueSet_MemberAbsentFromCodeSystem_ReturnsActiveNoIssue()
    {
        // Arrange
        var valueSetId = "test-vs-absent";
        var code = "vs-only-code";
        var system = "http://test.system";
        var display = "VS Only";

        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new() { Value = code, Display = display } } }
            }
        };
        // CodeSystem is loaded but does not contain the code.
        var codeSystemGroup = new CodeGroup
        {
            Url = system,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new CodeSystemCode { Value = "different", Display = "Different", Status = CodeStatus.Inactive } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system, It.IsAny<string>()))
            .Returns(codeSystemGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        Assert.Null(result.Parameter.FirstOrDefault(p => p.Name == "issues"));
    }

    [Fact]
    public void ValidateCodeInValueSet_ValueSetCodeInactive_OverridesActiveCodeSystem_ReturnsInactiveIssue()
    {
        // Arrange - the member carries its own inactive membership status (a 4-column value set file).
        // Even though the CodeSystem marks the code Active, the value set membership status wins.
        var valueSetId = "test-vs-member-inactive";
        var code = "member-inactive-code";
        var system = "http://test.system";
        var display = "Member Inactive Code";

        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new ValueSetCode { Value = code, Display = display, Status = CodeStatus.Inactive } } }
            }
        };
        // CodeSystem says Active; it must NOT be consulted because the membership status is authoritative.
        var codeSystemGroup = new CodeGroup
        {
            Url = system,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Active } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system, It.IsAny<string>()))
            .Returns(codeSystemGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert - membership status makes it inactive despite the active CodeSystem.
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        var issuesParameter = result.Parameter.FirstOrDefault(p => p.Name == "issues");
        Assert.NotNull(issuesParameter);
        var outcome = Assert.IsType<OperationOutcome>(issuesParameter.Resource);
        Assert.Equal("Code is inactive.", Assert.Single(outcome.Issue).Details?.Text);
    }

    [Fact]
    public void ValidateCodeInValueSet_ValueSetCodeActive_OverridesInactiveCodeSystem_ReturnsNoIssue()
    {
        // Arrange - the member carries its own active membership status; the CodeSystem is not consulted,
        // so an inactive CodeSystem status does not make an active value set member inactive.
        var valueSetId = "test-vs-member-active";
        var code = "member-active-code";
        var system = "http://test.system";
        var display = "Member Active Code";

        var valueSetGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new ValueSetCode { Value = code, Display = display, Status = CodeStatus.Active } } }
            }
        };
        // CodeSystem says Inactive; it must NOT be consulted because the membership status is authoritative.
        var codeSystemGroup = new CodeGroup
        {
            Url = system,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { system, new List<Code> { new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Inactive } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(valueSetGroup);
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system, It.IsAny<string>()))
            .Returns(codeSystemGroup);

        // Act
        var result = _service.ValidateCodeInValueSet(null, valueSetId, system, code, display, null);

        // Assert - active membership status means no inactive issue, despite the inactive CodeSystem.
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        Assert.Null(result.Parameter.FirstOrDefault(p => p.Name == "issues"));
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
    public void ValidateCodeInCodeSystem_WithInactiveCode_ReturnsInactiveIssueOutcome()
    {
        // Arrange
        var codeSystemId = "test-cs-inactive";
        var code = "inactive-code";
        var system = "http://test.system";
        var display = "Inactive Code";

        var mockCodeGroup = new CodeGroup
        {
            Id = codeSystemId,
            Url = system,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    system,
                    new List<Code>
                    {
                        new CodeSystemCode
                        {
                            Value = code,
                            Display = display,
                            Status = CodeStatus.Inactive
                        }
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

        var issuesParameter = result.Parameter.FirstOrDefault(p => p.Name == "issues");
        Assert.NotNull(issuesParameter);

        var outcome = Assert.IsType<OperationOutcome>(issuesParameter.Resource);
        var issue = Assert.Single(outcome.Issue);
        Assert.Equal(OperationOutcome.IssueSeverity.Warning, issue.Severity);
        Assert.Equal(OperationOutcome.IssueType.BusinessRule, issue.Code);
        Assert.Equal("Code is inactive.", issue.Details?.Text);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithDuplicateMatchesAndDisplay_SelectsLastMatchingDisplay()
    {
        // Arrange
        var codeSystemId = "test-cs-duplicate-display";
        var code = "duplicate-code";
        var system = "http://test.system";
        var display = "Matching Display";

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
                        new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Active },
                        new CodeSystemCode { Value = code, Display = "Other Display", Status = CodeStatus.Active },
                        new CodeSystemCode { Value = code, Display = display, Status = CodeStatus.Inactive },
                        new CodeSystemCode { Value = code, Display = "Last Entry", Status = CodeStatus.Active }
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
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        var outcome = Assert.IsType<OperationOutcome>(result.Parameter.Single(p => p.Name == "issues").Resource);
        Assert.Equal("Code is inactive.", Assert.Single(outcome.Issue).Details?.Text);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithDuplicateMatchesAndNoDisplay_SelectsLastEntry()
    {
        // Arrange
        var codeSystemId = "test-cs-duplicate-no-display";
        var code = "duplicate-code";
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
                        new CodeSystemCode { Value = code, Display = "First Entry", Status = CodeStatus.Active },
                        new CodeSystemCode { Value = code, Display = "Last Entry", Status = CodeStatus.Inactive }
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
        Assert.True(result.GetSingleValue<FhirBoolean>("result")?.Value);
        var outcome = Assert.IsType<OperationOutcome>(result.Parameter.Single(p => p.Name == "issues").Resource);
        Assert.Equal("Code is inactive.", Assert.Single(outcome.Issue).Details?.Text);
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

    [Fact]
    public void LookupCodeInCodeSystem_WithCodeAndSystem_ReturnsNameVersionDisplay()
    {
        // Arrange
        var codeSystemId = "lookup-cs-id";
        var codeSystemUrl = "http://lookup.system";
        var codeSystemVersion = "2.0.0";
        var code = "lookup-code";
        var display = "Lookup Display";
        var name = "LookupCodeSystem";

        var mockCodeGroup = new CodeGroup
        {
            Id = codeSystemId,
            Url = codeSystemUrl,
            Name = name,
            Version = codeSystemVersion,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { codeSystemUrl, new List<Code> { new() { Value = code, Display = display } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemUrl, codeSystemVersion))
            .Returns(mockCodeGroup);

        // Act
        var result = _service.LookupCodeInCodeSystem(null, null, codeSystemUrl, code, codeSystemVersion, null);

        // Assert
        Assert.Equal(name, result.GetSingleValue<FhirString>("name")?.Value);
        Assert.Equal(codeSystemVersion, result.GetSingleValue<FhirString>("version")?.Value);
        Assert.Equal(display, result.GetSingleValue<FhirString>("display")?.Value);
    }

    [Fact]
    public void LookupCodeInCodeSystem_WithCodingParameter_ReturnsNameVersionDisplay()
    {
        // Arrange
        var codeSystemId = "lookup-cs-id";
        var codeSystemUrl = "http://lookup.system";
        var codeSystemVersion = "3.1.4";
        var code = "lookup-code";
        var display = "Lookup Display";
        var name = "LookupCodeSystem";

        var mockCodeGroup = new CodeGroup
        {
            Id = codeSystemId,
            Url = codeSystemUrl,
            Name = name,
            Version = codeSystemVersion,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { codeSystemUrl, new List<Code> { new() { Value = code, Display = display } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemUrl, codeSystemVersion))
            .Returns(mockCodeGroup);

        var parameters = new Parameters();
        parameters.Add("version", new FhirString(codeSystemVersion));
        parameters.Add("coding", new Coding(codeSystemUrl, code, null));

        // Act
        var result = _service.LookupCodeInCodeSystem(null, null, null, null, null, parameters);

        // Assert
        Assert.Equal(name, result.GetSingleValue<FhirString>("name")?.Value);
        Assert.Equal(codeSystemVersion, result.GetSingleValue<FhirString>("version")?.Value);
        Assert.Equal(display, result.GetSingleValue<FhirString>("display")?.Value);
    }

    [Fact]
    public void LookupCodeInCodeSystem_WithVersionNoMatch_ThrowsKeyNotFoundException()
    {
        // Arrange
        var codeSystemUrl = "http://lookup.system";
        var requestedVersion = "9.9.9";
        var fallbackCodeGroup = new CodeGroup
        {
            Id = "lookup-cs-id",
            Url = codeSystemUrl,
            Name = "LookupCodeSystem",
            Version = "1.0.0",
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Codes = new Dictionary<string, List<Code>>
            {
                { codeSystemUrl, new List<Code> { new() { Value = "lookup-code", Display = "Lookup Display" } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, codeSystemUrl, requestedVersion))
            .Returns(fallbackCodeGroup);

        // Act / Assert
        Assert.Throws<KeyNotFoundException>(() =>
            _service.LookupCodeInCodeSystem(null, null, codeSystemUrl, "lookup-code", requestedVersion, null));
    }

    [Fact]
    public void LookupCodeInCodeSystem_WithCodeSystemAndCoding_ThrowsArgumentException()
    {
        // Arrange
        var parameters = new Parameters();
        parameters.Add("coding", new Coding("http://lookup.system", "lookup-code", null));

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            _service.LookupCodeInCodeSystem(null, null, "http://lookup.system", "lookup-code", null, parameters));
    }

    [Fact]
    public void GetMetaData_CodeSystemOperations_IncludesLookupOperation()
    {
        // Act
        var result = _service.GetMetaData();

        // Assert
        var codeSystemResource = result.Rest
            .SelectMany(rest => rest.Resource)
            .First(resource => resource.Type == "CodeSystem");

        Assert.Contains(codeSystemResource.Operation, operation =>
            operation.Name == "lookup" &&
            operation.Definition == "http://hl7.org/fhir/OperationDefinition/CodeSystem-lookup");
    }

    #endregion

    #region ExpandValueSet Tests

    [Fact]
    public void ExpandValueSet_WithMultipleSystems_IncludesCodesFromAllSystems()
    {
        // Arrange
        var valueSetId = "test-vs-expand";
        var system1 = "http://test.system/1";
        var system2 = "http://test.system/2";

        var mockCodeGroup = new CodeGroup
        {
            Id = valueSetId,
            Type = CodeGroup.CodeGroupTypes.ValueSet,
            Resource = new ValueSet { Id = valueSetId },
            Codes = new Dictionary<string, List<Code>>
            {
                { system1, new List<Code> { new() { Value = "code-1", Display = "Code One" } } },
                { system2, new List<Code> { new() { Value = "code-2", Display = "Code Two" } } }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, valueSetId, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Act
        var result = _service.ExpandValueSet(valueSetId, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Expansion);
        Assert.Equal(2, result.Expansion.Contains.Count);
        Assert.Contains(result.Expansion.Contains, c => c.System == system1 && c.Code == "code-1");
        Assert.Contains(result.Expansion.Contains, c => c.System == system2 && c.Code == "code-2");
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

    #region GetCodeSystems Tests

    [Fact]
    public void GetCodeSystems_WithDuplicateCode_EmitsSingleConceptWithLastDisplay()
    {
        // Arrange
        // The CSV lists the same code twice; a non-summary read must emit it once, keeping the
        // last occurrence's display (LEGLINK-814 dedup for $expand/read).
        var codeSystemId = "v3-ActCode";
        var url = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
        var code = "ACCTRECEIVABLE";

        var mockCodeGroup = new CodeGroup
        {
            Id = codeSystemId,
            Url = url,
            Type = CodeGroup.CodeGroupTypes.CodeSystem,
            Resource = new CodeSystem { Id = codeSystemId, Url = url },
            Codes = new Dictionary<string, List<Code>>
            {
                {
                    url,
                    new List<Code>
                    {
                        new CodeSystemCode { Value = code, Display = "first display", Status = CodeStatus.Active },
                        new CodeSystemCode { Value = code, Display = "last display", Status = CodeStatus.Inactive }
                    }
                }
            }
        };

        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, url, It.IsAny<string>()))
            .Returns(mockCodeGroup);

        // Act (summary omitted -> concepts are materialized)
        var result = _service.GetCodeSystems(url, null);

        // Assert
        var codeSystem = Assert.IsType<CodeSystem>(Assert.Single(result.Entry).Resource);
        var concept = Assert.Single(codeSystem.Concept);
        Assert.Equal(code, concept.Code);
        Assert.Equal("last display", concept.Display);
    }

    #endregion
}
