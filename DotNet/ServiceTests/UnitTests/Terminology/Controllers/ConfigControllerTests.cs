using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Controllers;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;

namespace UnitTests.Terminology;

/// <summary>
/// Unit tests for <see cref="ConfigController"/>'s cached-code lookups: <see cref="ConfigController.GetCodeSystemCode"/>
/// (LEGLINK-591) and its ValueSet counterpart <see cref="ConfigController.GetValueSetCode"/> (LEGLINK-889), both of
/// which return RFC 7807/9457 Problem Details for their validation and not-found edge cases instead of bare status
/// codes with plain-string bodies.
/// </summary>
/// <remarks>
/// No <see cref="Microsoft.AspNetCore.Http.HttpContext"/> is wired up: when the controller's
/// <see cref="Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory"/> is null (the
/// unit-testing scenario), <c>ControllerBase.Problem</c>/<c>ValidationProblem</c> build a plain
/// <see cref="ProblemDetails"/>/<see cref="ValidationProblemDetails"/> from their arguments, so the
/// results can be asserted directly. The runtime <c>traceId</c> is injected by the app's configured
/// <c>ProblemDetailsFactory</c> and is therefore out of scope for these tests.
///
/// The cache is mocked at <see cref="ICodeGroupCacheService"/> rather than at the concrete service, because the
/// value set lookup's effective-status rejoin goes through <c>GetCodeGroup</c>, which is not virtual. The
/// <see cref="FhirService"/> is real and shares the same mock, so the status the controller reports is resolved by
/// the same code <c>$validate-code</c> uses rather than by a stub that could drift from it.
/// </remarks>
public class ConfigControllerTests
{
    private readonly Mock<ICodeGroupCacheService> _mockCacheService;
    private readonly ConfigController _controller;

    private const string CodeSystemId = "v3-ActCode";
    private const string ValueSetId = "address-type";
    private const string SystemUri = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
    private const string AddressTypeSystem = "http://hl7.org/fhir/address-type";

    public ConfigControllerTests()
    {
        _mockCacheService = new Mock<ICodeGroupCacheService>();

        var fhirService = new FhirService(_mockCacheService.Object, Mock.Of<ILogger<FhirService>>());

        _controller = new ConfigController(
            _mockCacheService.Object, fhirService, Mock.Of<ILogger<ConfigController>>());
    }

    private static CodeGroup CodeSystemWithCodes(params Code[] codes) => new()
    {
        Type = CodeGroup.CodeGroupTypes.CodeSystem,
        Id = CodeSystemId,
        Url = SystemUri,
        Version = "1.0",
        Codes = new Dictionary<string, List<Code>> { [SystemUri] = codes.ToList() }
    };

    private static CodeGroup ValueSetWithCodes(params Code[] codes) =>
        ValueSetWithSystems(new Dictionary<string, List<Code>> { [AddressTypeSystem] = codes.ToList() });

    private static CodeGroup ValueSetWithSystems(Dictionary<string, List<Code>> codesBySystem) => new()
    {
        Type = CodeGroup.CodeGroupTypes.ValueSet,
        Id = ValueSetId,
        Url = "http://hl7.org/fhir/ValueSet/address-type",
        Version = "1.0",
        Codes = codesBySystem
    };

    private void GivenValueSet(CodeGroup? codeGroup, string? version = null) =>
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, version))
            .Returns(codeGroup);

    #region CodeSystem lookup

    [Fact]
    public void GetCodeSystemCode_MissingId_ReturnsValidationProblemWithIdError()
    {
        var result = _controller.GetCodeSystemCode(" ", "12345");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9457#section-3", problem.Type);
        Assert.Contains("id", problem.Errors.Keys);
    }

    [Fact]
    public void GetCodeSystemCode_MissingCode_ReturnsValidationProblemWithCodeError()
    {
        var result = _controller.GetCodeSystemCode(CodeSystemId, " ");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains("code", problem.Errors.Keys);
    }

    [Fact]
    public void GetCodeSystemCode_MissingIdAndCode_ReturnsValidationProblemWithBothErrors()
    {
        // Both errors are accumulated into ModelState before returning, rather than
        // short-circuiting on the first missing value.
        var result = _controller.GetCodeSystemCode(" ", " ");

        var problem = Assert.IsAssignableFrom<ValidationProblemDetails>(
            Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Contains("id", problem.Errors.Keys);
        Assert.Contains("code", problem.Errors.Keys);

        // Not-found lookups must never run when the request itself is invalid.
        _mockCacheService.Verify(
            x => x.GetCodeGroupById(It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public void GetCodeSystemCode_CodeSystemNotFound_Returns404ProblemDetails()
    {
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null))
            .Returns((CodeGroup?)null);

        var result = _controller.GetCodeSystemCode(CodeSystemId, "12345");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal("CodeSystem Not Found", problem.Title);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", problem.Type);
        Assert.Contains(CodeSystemId, problem.Detail);
        Assert.Contains("latest", problem.Detail);
    }

    [Fact]
    public void GetCodeSystemCode_CodeSystemNotFoundWithVersion_DetailIncludesVersion()
    {
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, "2.0"))
            .Returns((CodeGroup?)null);

        var result = _controller.GetCodeSystemCode(CodeSystemId, "12345", "2.0");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Contains("2.0", problem.Detail);

        // The requested version must be forwarded to the cache lookup.
        _mockCacheService.Verify(
            x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, "2.0"),
            Times.Once);
    }

    [Fact]
    public void GetCodeSystemCode_CodeNotFoundInCodeSystem_Returns404ProblemDetails()
    {
        var codeGroup = CodeSystemWithCodes(
            new CodeSystemCode { Value = "99999", Display = "Something else" });
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null))
            .Returns(codeGroup);

        var result = _controller.GetCodeSystemCode(CodeSystemId, "12345");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal("Code Not Found", problem.Title);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", problem.Type);
        Assert.Contains("12345", problem.Detail);
    }

    [Fact]
    public void GetCodeSystemCode_DuplicateCode_ReturnsLastOccurrence()
    {
        // A CSV may list the same code twice with differing status (Active then Inactive).
        // Last-one-wins (LEGLINK-599/814): the endpoint must return the last occurrence's status,
        // agreeing with $validate-code rather than returning the first (Active) entry.
        const string code = "ACCTRECEIVABLE";
        var codeGroup = CodeSystemWithCodes(
            new CodeSystemCode { Value = code, Display = "account receivable", Status = CodeStatus.Active },
            new CodeSystemCode { Value = code, Display = "account receivable", Status = CodeStatus.Inactive });
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null))
            .Returns(codeGroup);

        var result = _controller.GetCodeSystemCode(CodeSystemId, code);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var match = Assert.IsType<CodeSystemCode>(ok.Value);
        Assert.Equal(code, match.Value);
        Assert.Equal(CodeStatus.Inactive, match.Status);
    }

    [Fact]
    public void GetCodeSystemCode_Match_ReturnsOkWithCode()
    {
        // ProcessCodeSystemCsv only ever adds CodeSystemCode to a CodeSystem group, which is what lets the
        // endpoint declare CodeSystemCode as its response type and carry the status in the documented schema.
        var expected = new CodeSystemCode { Value = "12345", Display = "Matching code", Status = CodeStatus.Active };
        var codeGroup = CodeSystemWithCodes(
            new CodeSystemCode { Value = "99999", Display = "Something else" }, expected);
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null))
            .Returns(codeGroup);

        var result = _controller.GetCodeSystemCode(CodeSystemId, "12345");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var code = Assert.IsType<CodeSystemCode>(ok.Value);
        Assert.Equal("12345", code.Value);
        Assert.Equal("Matching code", code.Display);
        Assert.Equal(CodeStatus.Active, code.Status);
    }

    #endregion

    #region ValueSet lookup

    [Fact]
    public void GetValueSetCode_MissingIdAndCode_ReturnsValidationProblemWithBothErrors()
    {
        var result = _controller.GetValueSetCode(" ", " ");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Contains("id", problem.Errors.Keys);
        Assert.Contains("code", problem.Errors.Keys);

        _mockCacheService.Verify(
            x => x.GetCodeGroupById(It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public void GetValueSetCode_ValueSetNotFound_Returns404ProblemDetails()
    {
        GivenValueSet(null);

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal("ValueSet Not Found", problem.Title);
        Assert.Contains(ValueSetId, problem.Detail);
        Assert.Contains("latest", problem.Detail);
    }

    [Fact]
    public void GetValueSetCode_ValueSetNotFoundWithVersion_ForwardsVersionAndNamesItInDetail()
    {
        GivenValueSet(null, "2.0");

        var result = _controller.GetValueSetCode(ValueSetId, "postal", version: "2.0");

        var problem = Assert.IsAssignableFrom<ProblemDetails>(
            Assert.IsAssignableFrom<ObjectResult>(result.Result).Value);
        Assert.Contains("2.0", problem.Detail);

        _mockCacheService.Verify(
            x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, "2.0"),
            Times.Once);
    }

    [Fact]
    public void GetValueSetCode_CodeNotInValueSet_Returns404ProblemDetails()
    {
        GivenValueSet(ValueSetWithCodes(new ValueSetCode { Value = "physical", Display = "Physical" }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal("Code Not Found", problem.Title);
        Assert.Contains("postal", problem.Detail);
    }

    [Fact]
    public void GetValueSetCode_MembershipStatusInactive_ReportsInactiveWithoutConsultingCodeSystem()
    {
        // The LEGLINK-889 fixture: 'postal' marked inactive in the four-column value set CSV. The membership
        // status is authoritative, so the code system is never consulted and cannot contradict it.
        GivenValueSet(ValueSetWithCodes(
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lookup = Assert.IsType<ValueSetCodeLookupResult>(ok.Value);
        Assert.Equal(AddressTypeSystem, lookup.System);
        Assert.Equal("postal", lookup.Value);
        Assert.Equal("Postal", lookup.Display);
        Assert.Equal(CodeStatus.Inactive, lookup.MembershipStatus);
        Assert.Equal(CodeStatus.Inactive, lookup.EffectiveStatus);

        _mockCacheService.Verify(
            x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public void GetValueSetCode_MembershipStatusActive_OverridesAnInactiveCodeSystem()
    {
        // Membership status wins in both directions: a value set may keep a code its code system has retired.
        GivenValueSet(ValueSetWithCodes(
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Active }));
        GivenCodeSystemStatus(CodeStatus.Inactive);

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(CodeStatus.Active, lookup.MembershipStatus);
        Assert.Equal(CodeStatus.Active, lookup.EffectiveStatus);
    }

    [Fact]
    public void GetValueSetCode_NoMembershipStatus_RejoinsStatusFromCodeSystem()
    {
        // A three-column value set CSV loads plain Code objects, so the value set declares nothing about the
        // member and the effective status comes from the code system. MembershipStatus stays null to say so.
        GivenValueSet(ValueSetWithCodes(new Code { Value = "postal", Display = "Postal" }));
        GivenCodeSystemStatus(CodeStatus.Inactive);

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(lookup.MembershipStatus);
        Assert.Equal(CodeStatus.Inactive, lookup.EffectiveStatus);
    }

    [Fact]
    public void GetValueSetCode_NoMembershipStatusAndNoCodeSystemLoaded_ReportsActive()
    {
        // Nothing to rejoin from: the pre-LEGLINK-639 default of "assume active" is preserved.
        GivenValueSet(ValueSetWithCodes(new Code { Value = "postal", Display = "Postal" }));
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, AddressTypeSystem, null))
            .Returns((CodeGroup?)null);

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Null(lookup.MembershipStatus);
        Assert.Equal(CodeStatus.Active, lookup.EffectiveStatus);
    }

    [Fact]
    public void GetValueSetCode_DuplicateCode_ReturnsLastOccurrence()
    {
        // Same last-one-wins rule as the code system lookup (LEGLINK-599/814).
        GivenValueSet(ValueSetWithCodes(
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Active },
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(CodeStatus.Inactive, lookup.MembershipStatus);
    }

    [Fact]
    public void GetValueSetCode_NoSystemSupplied_TakesFirstSystemListingTheCode()
    {
        // Mirrors FhirService.ValidateCodeAcrossSystems: systems are walked in load order and the first one
        // listing the code wins, so the endpoint and $validate-code name the same occurrence.
        const string otherSystem = "http://example.org/other";
        GivenValueSet(ValueSetWithSystems(new Dictionary<string, List<Code>>
        {
            [otherSystem] = [new ValueSetCode { Value = "postal", Display = "Other postal", Status = CodeStatus.Active }],
            [AddressTypeSystem] = [new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }]
        }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(otherSystem, lookup.System);
        Assert.Equal(CodeStatus.Active, lookup.MembershipStatus);
    }

    [Fact]
    public void GetValueSetCode_SystemSupplied_RestrictsTheSearchToThatSystem()
    {
        const string otherSystem = "http://example.org/other";
        GivenValueSet(ValueSetWithSystems(new Dictionary<string, List<Code>>
        {
            [otherSystem] = [new ValueSetCode { Value = "postal", Display = "Other postal", Status = CodeStatus.Active }],
            [AddressTypeSystem] = [new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }]
        }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal", system: AddressTypeSystem);

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(AddressTypeSystem, lookup.System);
        Assert.Equal(CodeStatus.Inactive, lookup.MembershipStatus);
    }

    [Fact]
    public void GetValueSetCode_SystemNotInValueSet_Returns404NamingTheSystem()
    {
        GivenValueSet(ValueSetWithCodes(
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal", system: "http://example.org/absent");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Contains("http://example.org/absent", problem.Detail);
    }

    [Fact]
    public void GetValueSetCode_BlankSystem_IsTreatedAsOmitted()
    {
        GivenValueSet(ValueSetWithCodes(
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal", system: "  ");

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(AddressTypeSystem, lookup.System);
    }

    private void GivenCodeSystemStatus(CodeStatus status) =>
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, AddressTypeSystem, null))
            .Returns(new CodeGroup
            {
                Type = CodeGroup.CodeGroupTypes.CodeSystem,
                Id = "address-type",
                Url = AddressTypeSystem,
                Version = "1.0",
                Codes = new Dictionary<string, List<Code>>
                {
                    [AddressTypeSystem] = [new CodeSystemCode { Value = "postal", Display = "Postal", Status = status }]
                }
            });

    #endregion
}
