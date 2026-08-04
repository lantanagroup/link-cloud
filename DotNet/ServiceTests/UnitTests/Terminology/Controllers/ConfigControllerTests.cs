using System.Globalization;
using CsvHelper;
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
using Task = System.Threading.Tasks.Task;

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
            _mockCacheService.Object, options.Object, Mock.Of<ILogger<ConfigController>>());
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

    #region Code upload

    private const string ValueSetId = "v3-ActEncounterCode";

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

        var result = await controller.ReplaceValueSetCodes(ValueSetId, BuildCsvFile("system,code,display\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status404NotFound);
        Assert.Equal("Not Found", problem.Title);
        // Indistinguishable from a route that was never deployed.
        Assert.Equal("The requested terminology endpoint was not found.", problem.Detail);
        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()),
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
        var result = await _controller.ReplaceValueSetCodes(ValueSetId, file: null);

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains("file", Assert.IsType<ValidationProblemDetails>(problem).Errors.Keys);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_EmptyFile_Returns400WithFileError()
    {
        var result = await _controller.ReplaceValueSetCodes(ValueSetId, BuildCsvFile(string.Empty));

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains("file", Assert.IsType<ValidationProblemDetails>(problem).Errors.Keys);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_NonCsvExtension_Returns400WithFileError()
    {
        var result = await _controller.ReplaceValueSetCodes(
            ValueSetId, BuildCsvFile("system,code,display\r\n", "codes.txt"));

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
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, null, It.IsAny<string>()))
            .Throws(new KeyNotFoundException("No ValueSet found in the cache with id 'x' and version 'latest'"));

        var result = await _controller.ReplaceValueSetCodes(ValueSetId, BuildCsvFile("system,code,display\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status404NotFound);
        Assert.Equal("ValueSet Not Found", problem.Title);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_WrongColumnCount_Returns400WithTheColumnMessage()
    {
        const string message = "ValueSet CSV must have 3 or 4 columns: system, code, display, and optionally status.";
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, null, It.IsAny<string>()))
            .Throws(new InvalidOperationException(message));

        var result = await _controller.ReplaceValueSetCodes(ValueSetId, BuildCsvFile("a,b\r\n1,2\r\n"));

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
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, null, It.IsAny<string>()))
            .Throws(new CsvHelperException(
                csvReader.Context, $"The conversion cannot be performed. Text: '{secret}'"));

        var result = await _controller.ReplaceValueSetCodes(ValueSetId, BuildCsvFile("system,code,display\r\nx,y,z\r\n"));

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        // CsvHelper embeds the offending field in its message; that text is caller-supplied.
        Assert.DoesNotContain(secret, problem.Detail);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_Success_Returns202WithCounts()
    {
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, null, It.IsAny<string>()))
            .Returns(new CodeGroup
            {
                Type = CodeGroup.CodeGroupTypes.ValueSet,
                Id = ValueSetId,
                Version = "3.0.0",
                Codes = new Dictionary<string, List<Code>>
                {
                    ["http://a"] = [new ValueSetCode { Value = "1", Display = "One", Status = CodeStatus.Inactive }],
                    ["http://b"] = [new ValueSetCode { Value = "2", Display = "Two", Status = CodeStatus.Active }]
                }
            });

        var result = await _controller.ReplaceValueSetCodes(
            ValueSetId, BuildCsvFile("system,code,display,status\r\n"));

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var body = Assert.IsType<ReplaceCodesResponse>(accepted.Value);
        Assert.Equal("ValueSet", body.Type);
        Assert.Equal(ValueSetId, body.Id);
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
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null, csv))
            .Returns(CodeSystemWithCodes(new CodeSystemCode { Value = "ZZZ", Display = "Injected", Status = CodeStatus.Active }));

        var result = await _controller.ReplaceCodeSystemCodes(CodeSystemId, BuildCsvFile(csv));

        Assert.IsType<AcceptedResult>(result.Result);
        // The route determines the type, so a CSV can never be applied to the wrong kind of group.
        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemId, null, csv), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReplaceValueSetCodes_BlankVersion_IsTreatedAsLatest(string? version)
    {
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, null, It.IsAny<string>()))
            .Returns(new CodeGroup { Type = CodeGroup.CodeGroupTypes.ValueSet, Id = ValueSetId });

        await _controller.ReplaceValueSetCodes(ValueSetId, BuildCsvFile("system,code,display\r\n"), version);

        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ReplaceValueSetCodes_ExplicitVersion_IsForwarded()
    {
        _mockCacheService
            .Setup(x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, "2.0", It.IsAny<string>()))
            .Returns(new CodeGroup { Type = CodeGroup.CodeGroupTypes.ValueSet, Id = ValueSetId });

        await _controller.ReplaceValueSetCodes(ValueSetId, BuildCsvFile("system,code,display\r\n"), "2.0");

        _mockCacheService.Verify(
            x => x.ReplaceCodesFromCsv(CodeGroup.CodeGroupTypes.ValueSet, ValueSetId, "2.0", It.IsAny<string>()),
            Times.Once);
    }

    #endregion
}
