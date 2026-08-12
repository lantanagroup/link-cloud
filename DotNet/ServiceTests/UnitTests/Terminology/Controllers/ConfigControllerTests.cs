using System.Globalization;
using CsvHelper;
using LantanaGroup.Link.Terminology.Application.Exceptions;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Controllers;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Terminology;

/// <summary>
/// Unit tests for <see cref="ConfigController"/>: the cached-code lookups
/// (<see cref="ConfigController.GetCodeSystemCode"/>, LEGLINK-591, and its ValueSet counterpart
/// <see cref="ConfigController.GetValueSetCode"/>, LEGLINK-889), which return RFC 7807/9457 Problem Details for
/// their validation and not-found edge cases instead of bare status codes with plain-string bodies, and the
/// non-production CSV upload endpoints that replace a cached code group's codes in memory.
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
    private readonly FhirService _fhirService;
    private readonly ConfigController _controller;

    private const string CodeSystemId = "v3-ActCode";
    private const string ValueSetId = "address-type";
    private const string SystemUri = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
    private const string AddressTypeSystem = "http://hl7.org/fhir/address-type";

    public ConfigControllerTests()
    {
        _mockCacheService = new Mock<ICodeGroupCacheService>();
        _fhirService = new FhirService(_mockCacheService.Object, Mock.Of<ILogger<FhirService>>());

        // The upload endpoints are enabled for most tests; BuildController(false) covers the
        // production configuration where the feature is off.
        _controller = BuildController(enableCodeUpload: true);
    }

    private ConfigController BuildController(bool enableCodeUpload)
    {
        var options = new Mock<IOptions<TerminologyConfig>>();
        options.Setup(x => x.Value).Returns(new TerminologyConfig
        {
            Path = "/test/path",
            EnableCodeUploadEndpoint = enableCodeUpload
        });

        return new ConfigController(
            _mockCacheService.Object, _fhirService, options.Object, Mock.Of<ILogger<ConfigController>>());
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
        // Mirrors FhirService.ValidateCodeAcrossSystems: systems are walked in enumeration order and the
        // first one listing the code wins, so the endpoint and $validate-code name the same occurrence.
        const string otherSystem = "http://example.org/other";
        var codeGroup = ValueSetWithSystems(new Dictionary<string, List<Code>>
        {
            [otherSystem] = [new ValueSetCode { Value = "postal", Display = "Other postal", Status = CodeStatus.Active }],
            [AddressTypeSystem] = [new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }]
        });
        GivenValueSet(codeGroup);

        // Both systems list the code, so which one wins is decided entirely by the order the group
        // enumerates - and Dictionary does not contract that order. The expectation is therefore read from
        // the same walk FindValueSetMember makes rather than naming a key, so the assertion is "the first
        // system listing the code wins" and not "this particular system wins". Picking the last system, or
        // an arbitrary one, still fails.
        var expectedSystem = codeGroup.Codes.First(entry => entry.Value.Any(c => c.Value == "postal")).Key;
        var expectedStatus = ((ValueSetCode)codeGroup.Codes[expectedSystem].Last(c => c.Value == "postal")).Status;

        var result = _controller.GetValueSetCode(ValueSetId, "postal");

        var lookup = Assert.IsType<ValueSetCodeLookupResult>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(expectedSystem, lookup.System);
        Assert.Equal(expectedStatus, lookup.MembershipStatus);
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

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<style>a{}</style>")]
    public void GetValueSetCode_SystemSanitizesAwayToNothing_Returns400RatherThanWideningTheSearch(string system)
    {
        // These are not whitespace, so the "treat blank as omitted" path does not catch them, but the
        // sanitizer drops the element and its content and leaves nothing. Quietly treating that as an omitted
        // system would turn a single-system lookup into a search across every system in the value set and
        // could answer with a member the caller never asked about, so it is rejected. The cache must not be
        // consulted at all.
        GivenValueSet(ValueSetWithCodes(
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal", system: system);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains("system", problem.Errors.Keys);

        _mockCacheService.Verify(
            x => x.GetCodeGroupById(It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public void GetValueSetCode_SystemSurvivesSanitizing_IsLookedUpNotRejected()
    {
        // The 400 above is narrow on purpose. HtmlSanitizer ships an allow-list, and "b" is on it, so
        // "<b></b>" comes back intact rather than empty. That is a system the value set simply does not
        // list, which is a 404 - not a rejected parameter. Pinning this keeps the guard from drifting into
        // "anything that looks like markup is a 400", which would start rejecting real system URIs.
        GivenValueSet(ValueSetWithCodes(
            new ValueSetCode { Value = "postal", Display = "Postal", Status = CodeStatus.Inactive }));

        var result = _controller.GetValueSetCode(ValueSetId, "postal", system: "<b></b>");

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal("Code Not Found", problem.Title);
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

    #region Code upload

    // A different value set from the lookup tests' address-type on purpose: the upload tests only ever reach a
    // mocked ReplaceCodesFromCsv, so nothing about the fixture matters beyond the id being passed through
    // unchanged, and keeping them distinct stops a lookup stub from accidentally satisfying an upload test.
    private const string UploadValueSetId = "v3-ActEncounterCode";

    private static IFormFile BuildCsvFile(string content, string fileName = "codes.csv")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }

    private static ProblemDetails AssertProblem(ActionResult<ReplaceCodesResponse> result, int expectedStatus)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        return Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_FeatureDisabled_Returns404AndNeverTouchesTheCache()
    {
        var controller = BuildController(enableCodeUpload: false);

        var result = await controller.ReplaceValueSetCodes(UploadValueSetId, BuildCsvFile("system,code,display\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status404NotFound);
        Assert.Equal("Not Found", problem.Title);
        // Indistinguishable from a route that was never deployed.
        Assert.Equal("The requested terminology endpoint was not found.", problem.Detail);
        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceCodeSystemCodes_FeatureDisabled_Returns404()
    {
        var controller = BuildController(enableCodeUpload: false);

        var result = await controller.ReplaceCodeSystemCodes(CodeSystemId, BuildCsvFile("code,display\r\n"));

        AssertProblem(result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_NoFile_Returns400WithFileError()
    {
        var result = await _controller.ReplaceValueSetCodes(UploadValueSetId, file: null);

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains("file", Assert.IsType<ValidationProblemDetails>(problem).Errors.Keys);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_EmptyFile_Returns400WithFileError()
    {
        var result = await _controller.ReplaceValueSetCodes(UploadValueSetId, BuildCsvFile(string.Empty));

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains("file", Assert.IsType<ValidationProblemDetails>(problem).Errors.Keys);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_NonCsvExtension_Returns400WithFileError()
    {
        var result = await _controller.ReplaceValueSetCodes(
            UploadValueSetId, BuildCsvFile("system,code,display\r\n", "codes.txt"));

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains("file", Assert.IsType<ValidationProblemDetails>(problem).Errors.Keys);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_BlankId_Returns400WithIdError()
    {
        var result = await _controller.ReplaceValueSetCodes(" ", BuildCsvFile("system,code,display\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains("id", Assert.IsType<ValidationProblemDetails>(problem).Errors.Keys);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_BlankIdAndBadFile_ReportsBothErrors()
    {
        var result = await _controller.ReplaceValueSetCodes(" ", file: null);

        var problem = Assert.IsType<ValidationProblemDetails>(AssertProblem(result, StatusCodes.Status400BadRequest));
        Assert.Contains("id", problem.Errors.Keys);
        Assert.Contains("file", problem.Errors.Keys);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_UnknownTarget_Returns404WithTypedTitle()
    {
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new CodeGroupNotFoundException("No ValueSet found in the cache with id 'x' and version 'latest'"));

        var result = await _controller.ReplaceValueSetCodes(UploadValueSetId, BuildCsvFile("system,code,display\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status404NotFound);
        Assert.Equal("ValueSet Not Found", problem.Title);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_WrongColumnCount_Returns400WithTheColumnMessage()
    {
        const string message = "ValueSet CSV must have 3 or 4 columns: system, code, display, and optionally status.";
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException(message));

        var result = await _controller.ReplaceValueSetCodes(UploadValueSetId, BuildCsvFile("a,b\r\n1,2\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        // The message is a fixed literal describing the expected columns, so echoing it is safe.
        Assert.Equal(message, problem.Detail);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_MalformedCsv_Returns400WithoutEchoingTheOffendingField()
    {
        const string secret = "PATIENT-SSN-123-45-6789";
        using var csvReader = new CsvReader(new StringReader("a,b\r\n1,2\r\n"), CultureInfo.InvariantCulture);
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new CsvHelperException(
                csvReader.Context, $"The conversion cannot be performed. Text: '{secret}'"));

        var result = await _controller.ReplaceValueSetCodes(UploadValueSetId, BuildCsvFile("system,code,display\r\nx,y,z\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        // CsvHelper embeds the offending field in its message; that text is caller-supplied.
        Assert.DoesNotContain(secret, problem.Detail);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_Success_Returns202WithCounts()
    {
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new CodeGroup
            {
                Type = CodeGroup.CodeGroupTypes.ValueSet,
                Id = UploadValueSetId,
                Version = "3.0.0",
                Codes = new Dictionary<string, List<Code>>
                {
                    ["http://a"] = [new ValueSetCode { Value = "1", Display = "One", Status = CodeStatus.Inactive }],
                    ["http://b"] = [new ValueSetCode { Value = "2", Display = "Two", Status = CodeStatus.Active }]
                }
            });

        var result = await _controller.ReplaceValueSetCodes(
            UploadValueSetId, BuildCsvFile("system,code,display,status\r\n"));

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var body = Assert.IsType<ReplaceCodesResponse>(accepted.Value);
        Assert.Equal("ValueSet", body.Type);
        Assert.Equal(UploadValueSetId, body.Id);
        Assert.Equal("3.0.0", body.Version);
        Assert.Equal(2, body.CodeCount);
        Assert.Equal(2, body.SystemCount);
        Assert.Equal(1, body.InactiveCodeCount);
        Assert.Equal("codes.csv", body.FileName);
    }

    [Fact]
    public async Task ReplaceCodeSystemCodes_Success_PassesCodeSystemTypeAndCsvContent()
    {
        const string csv = "code,display\r\nZZZ,Injected\r\n";
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null, csv, It.IsAny<CancellationToken>()))
            .Returns(CodeSystemWithCodes(new CodeSystemCode { Value = "ZZZ", Display = "Injected", Status = CodeStatus.Active }));

        var result = await _controller.ReplaceCodeSystemCodes(CodeSystemId, BuildCsvFile(csv));

        Assert.IsType<AcceptedResult>(result.Result);
        // The route determines the type, so a CSV can never be applied to the wrong kind of group.
        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null, csv, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_ForwardsTheRequestCancellationTokenToTheCache()
    {
        // The parse is synchronous and walks the CSV row by row, so the token the endpoint was handed is the
        // only thing that lets a client disconnect stop it. It.IsAny<CancellationToken>() in the other tests
        // would pass just as happily against the four-argument overload, so this asserts the exact token.
        using var cts = new CancellationTokenSource();

        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(
                CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), cts.Token))
            .Returns(new CodeGroup { Type = CodeGroup.CodeGroupTypes.ValueSet, Id = UploadValueSetId });

        await _controller.ReplaceValueSetCodes(
            UploadValueSetId, BuildCsvFile("system,code,display\r\n"), version: null, cancellationToken: cts.Token);

        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(
                CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), cts.Token),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReplaceValueSetCodes_BlankVersion_IsTreatedAsLatest(string? version)
    {
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new CodeGroup { Type = CodeGroup.CodeGroupTypes.ValueSet, Id = UploadValueSetId });

        await _controller.ReplaceValueSetCodes(UploadValueSetId, BuildCsvFile("system,code,display\r\n"), version);

        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, UploadValueSetId, null, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
