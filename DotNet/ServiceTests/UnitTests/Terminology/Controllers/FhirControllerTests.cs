using Hl7.Fhir.Model;
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
/// Unit tests for the request handling in <see cref="FhirController"/>'s $validate-code actions:
/// how untrusted query parameters are sanitized before they are compared against loaded terminology
/// (LEGLINK-886) and which malformed requests are rejected with a 400 (LEGLINK-887).
/// </summary>
public class FhirControllerTests
{
    private readonly Mock<ICodeGroupCacheService> _mockCacheService;
    private readonly FhirController _controller;

    private const string ValueSetUrl = "http://test.org/ValueSet/loinc-subset";
    private const string CodeSystemUrl = "http://loinc.org";
    private const string LoincCode = "100105-6";

    // A display carrying a character the HTML sanitizer would otherwise encode. Over 2,000 LOINC
    // displays contain an ampersand, so this is the common case rather than an exotic one.
    private const string DisplayWithAmpersand = "Filaria Ab.IgG & IgM panel";

    public FhirControllerTests()
    {
        _mockCacheService = new Mock<ICodeGroupCacheService>();
        var service = new FhirService(_mockCacheService.Object, new Mock<ILogger<FhirService>>().Object);
        _controller = new FhirController(service);
    }

    private static CodeGroup BuildCodeGroup(CodeGroup.CodeGroupTypes type, string system) => new()
    {
        Id = "test-group",
        Type = type,
        Url = system,
        Codes = new Dictionary<string, List<Code>>
        {
            { system, new List<Code> { new() { Value = LoincCode, Display = DisplayWithAmpersand } } }
        }
    };

    private static Parameters AssertOkParameters(ActionResult<Parameters> result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<Parameters>(okResult.Value);
    }

    /// <summary>
    /// Asserts that the action produced an RFC 9457 Problem Details 400 carrying <paramref name="expectedDetail"/>.
    /// </summary>
    /// <remarks>
    /// No <see cref="HttpContext"/> is wired up, so the controller's <c>ProblemDetailsFactory</c> is null and
    /// <c>ControllerBase.Problem</c> builds a plain <see cref="ProblemDetails"/> from its arguments. The runtime
    /// <c>traceId</c> is injected by the app's configured factory and is out of scope here (see ConfigControllerTests).
    /// </remarks>
    private static void AssertBadRequestProblem(ActionResult<Parameters> result, string expectedDetail)
    {
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.Equal(expectedDetail, problem.Detail);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithDisplayContainingAmpersand_ReturnsTrue()
    {
        // Arrange
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(BuildCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, CodeSystemUrl));

        // Act
        var result = _controller.ValidateCodeInValueSet(ValueSetUrl, null, CodeSystemUrl, LoincCode, DisplayWithAmpersand, null);

        // Assert - encoding the "&" would report a spurious display mismatch
        var parameters = AssertOkParameters(result);
        Assert.True(parameters.GetSingleValue<FhirBoolean>("result")?.Value);
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithDisplayContainingAmpersand_ReturnsTrue()
    {
        // Arrange
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemUrl, It.IsAny<string>()))
            .Returns(BuildCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemUrl));

        // Act
        var result = _controller.ValidateCodeInCodeSystem(CodeSystemUrl, null, LoincCode, DisplayWithAmpersand, null);

        // Assert
        var parameters = AssertOkParameters(result);
        Assert.True(parameters.GetSingleValue<FhirBoolean>("result")?.Value);
    }

    [Fact]
    public void ValidateCodeInValueSet_WithMarkupOnlyDisplay_ReturnsBadRequest()
    {
        // Arrange
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(BuildCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, CodeSystemUrl));

        // Act
        var result = _controller.ValidateCodeInValueSet(
            ValueSetUrl, null, CodeSystemUrl, LoincCode, "<script>alert('x')</script>", null);

        // Assert - the display sanitizes away to nothing; passing the empty value on would skip the
        // display check and answer result=true, so the request is rejected instead
        AssertBadRequestProblem(result, "Invalid value supplied for 'display'.");
    }

    [Fact]
    public void ValidateCodeInCodeSystem_WithMarkupOnlyDisplay_ReturnsBadRequest()
    {
        // Arrange
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemUrl, It.IsAny<string>()))
            .Returns(BuildCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, CodeSystemUrl));

        // Act
        var result = _controller.ValidateCodeInCodeSystem(
            CodeSystemUrl, null, LoincCode, "<script>alert('x')</script>", null);

        // Assert
        AssertBadRequestProblem(result, "Invalid value supplied for 'display'.");
    }

    [Fact]
    public void ValidateCodeInValueSet_WithNoUrlOrId_ReturnsBadRequest()
    {
        // Act
        var result = _controller.ValidateCodeInValueSet(null, null, null, LoincCode, null, null);

        // Assert
        AssertBadRequestProblem(result, "No id or url parameter specified.");
    }

    [Fact]
    public void ValidateCodeInValueSet_WithEmptyUrl_ReturnsBadRequest()
    {
        // Act
        var result = _controller.ValidateCodeInValueSet(string.Empty, null, null, LoincCode, null, null);

        // Assert
        AssertBadRequestProblem(result, "No id or url parameter specified.");
    }

    [Fact]
    public void ValidateCodeInValueSet_WithEmptyValueUriInBody_ReturnsBadRequest()
    {
        // Arrange - LEGLINK-887's reported request: the url arrives in the POST body as an empty valueUri
        var parameters = new Parameters();
        parameters.Add("url", new FhirUri(string.Empty));
        parameters.Add("code", new FhirString(LoincCode));

        // Act
        var result = _controller.ValidateCodeInValueSet(null, null, null, null, null, parameters);

        // Assert
        AssertBadRequestProblem(result, "No id or url parameter specified.");
    }
}
