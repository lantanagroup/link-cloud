using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using LantanaGroup.Link.Terminology.Controllers;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;

namespace UnitTests.Terminology;

/// <summary>
/// Unit tests for <see cref="ConfigController.GetCodeSystemCode"/> (LEGLINK-591), which returns
/// RFC 7807/9457 Problem Details for its validation and not-found edge cases instead of bare
/// status codes with plain-string bodies.
/// </summary>
/// <remarks>
/// No <see cref="Microsoft.AspNetCore.Http.HttpContext"/> is wired up: when the controller's
/// <see cref="Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory"/> is null (the
/// unit-testing scenario), <c>ControllerBase.Problem</c>/<c>ValidationProblem</c> build a plain
/// <see cref="ProblemDetails"/>/<see cref="ValidationProblemDetails"/> from their arguments, so the
/// results can be asserted directly. The runtime <c>traceId</c> is injected by the app's configured
/// <c>ProblemDetailsFactory</c> and is therefore out of scope for these tests.
/// </remarks>
public class ConfigControllerTests
{
    private readonly Mock<CodeGroupCacheService> _mockCacheService;
    private readonly ConfigController _controller;

    private const string CodeSystemId = "v3-ActCode";
    private const string SystemUri = "http://terminology.hl7.org/CodeSystem/v3-ActCode";

    public ConfigControllerTests()
    {
        var mockCacheLogger = new Mock<ILogger<CodeGroupCacheService>>();
        var mockCache = new Mock<IMemoryCache>();
        var mockConfig = new Mock<IOptions<TerminologyConfig>>();
        mockConfig.Setup(x => x.Value).Returns(new TerminologyConfig { Path = "/test/path" });

        // GetCodeGroupById is virtual, so the concrete service can be mocked directly.
        _mockCacheService = new Mock<CodeGroupCacheService>(
            mockCacheLogger.Object, mockCache.Object, mockConfig.Object);

        _controller = new ConfigController(_mockCacheService.Object, Mock.Of<ILogger<ConfigController>>());
    }

    private static CodeGroup CodeSystemWithCodes(params Code[] codes) => new()
    {
        Type = CodeGroup.CodeGroupTypes.CodeSystem,
        Id = CodeSystemId,
        Url = SystemUri,
        Version = "1.0",
        Codes = new Dictionary<string, List<Code>> { [SystemUri] = codes.ToList() }
    };

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
        var codeGroup = CodeSystemWithCodes(new Code { Value = "99999", Display = "Something else" });
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
        var expected = new Code { Value = "12345", Display = "Matching code" };
        var codeGroup = CodeSystemWithCodes(
            new Code { Value = "99999", Display = "Something else" }, expected);
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null))
            .Returns(codeGroup);

        var result = _controller.GetCodeSystemCode(CodeSystemId, "12345");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var code = Assert.IsType<Code>(ok.Value);
        Assert.Equal("12345", code.Value);
        Assert.Equal("Matching code", code.Display);
    }
}
