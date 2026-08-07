using Hl7.Fhir.Model;
using LantanaGroup.Link.Terminology.Application.Extensions;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Controllers;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    /// Asserts that the action produced an RFC 9457 Problem Details result with the given status, title,
    /// type and detail.
    /// </summary>
    /// <remarks>
    /// No <see cref="HttpContext"/> is wired up, so the controller's <c>ProblemDetailsFactory</c> is null and
    /// <c>ControllerBase.Problem</c> builds a plain <see cref="ProblemDetails"/> from its arguments. The runtime
    /// <c>traceId</c> extension and the scrubbing of 5xx detail are applied by the configured customization,
    /// which is covered separately below (see ConfigControllerTests for the same note).
    /// </remarks>
    private static void AssertProblem(
        ActionResult? result, int expectedStatus, string expectedTitle, string expectedType, string expectedDetail)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal(expectedType, problem.Type);
        Assert.Equal(expectedDetail, problem.Detail);
    }

    private static void AssertBadRequestProblem(ActionResult<Parameters> result, string expectedDetail) =>
        AssertProblem(result.Result, StatusCodes.Status400BadRequest, "Bad Request",
            "https://tools.ietf.org/html/rfc9110#section-15.5.1", expectedDetail);

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

    [Fact]
    public void ValidateCodeInValueSet_WithBlankSystemInCodingBody_ReturnsBadRequest()
    {
        // Arrange - LEGLINK-888's reported request: a resolvable value set and a code that does match,
        // with the coding's system blank. The value set is wired up deliberately, so the 400 proves the
        // blank is rejected rather than the request merely failing to find anything.
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(BuildCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, CodeSystemUrl));

        var parameters = new Parameters();
        parameters.Add("url", new FhirUri(ValueSetUrl));
        parameters.Add("coding", new Coding { Code = LoincCode, System = string.Empty });

        // Act
        var result = _controller.ValidateCodeInValueSet(null, null, null, null, null, parameters);

        // Assert - previously this answered 200 result=true by searching every system in the value set
        AssertBadRequestProblem(result, "The 'coding.system' parameter cannot be blank.");
    }

    [Fact]
    public void ValidateCodeInValueSet_WithBlankSystemQueryParameter_ReturnsBadRequest()
    {
        // Arrange
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(BuildCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, CodeSystemUrl));

        // Act - an empty query string value binds as "", not as an absent parameter
        var result = _controller.ValidateCodeInValueSet(ValueSetUrl, null, string.Empty, LoincCode, null, null);

        // Assert
        AssertBadRequestProblem(result, "The 'system' parameter cannot be blank.");
    }

    [Fact]
    public void ValidateCodeInValueSet_WithNullPlaceholderSystem_SearchesAllSystems()
    {
        // Arrange
        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(BuildCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, CodeSystemUrl));

        // Act - a client that interpolated an unset variable sends the literal string "null"
        var result = _controller.ValidateCodeInValueSet(ValueSetUrl, null, "null", LoincCode, null, null);

        // Assert - treated as if the system had been omitted rather than looked up as a system URL
        var parameters = AssertOkParameters(result);
        Assert.True(parameters.GetSingleValue<FhirBoolean>("result")?.Value);
    }

    [Fact]
    public void GetValueSetById_WhenValueSetNotLoaded_ReturnsNotFoundProblem()
    {
        // Arrange - the cache has no value set under this id
        _mockCacheService
            .Setup(x => x.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, "missing-vs", It.IsAny<string>()))
            .Returns((CodeGroup?)null);

        // Act
        var result = _controller.GetValueSetById("missing-vs");

        // Assert
        AssertProblem(result.Result, StatusCodes.Status404NotFound, "Not Found",
            "https://tools.ietf.org/html/rfc9110#section-15.5.5", "Value set not found with ID missing-vs.");
    }

    [Fact]
    public void GetValueSets_WhenCachedResourceIsNotAValueSet_ReturnsInternalServerErrorProblem()
    {
        // Arrange - a code group cached under the ValueSet type whose resource is a CodeSystem
        var mismatched = BuildCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, CodeSystemUrl);
        mismatched.Resource = new CodeSystem { Id = "not-a-value-set", Url = CodeSystemUrl };

        _mockCacheService
            .Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, ValueSetUrl, It.IsAny<string>()))
            .Returns(mismatched);

        // Act
        var result = _controller.GetValueSets(ValueSetUrl, null);

        // Assert - the controller sets the 5xx contract; the customization scrubs the detail at runtime
        AssertProblem(result.Result, StatusCodes.Status500InternalServerError, "Internal Server Error",
            "https://tools.ietf.org/html/rfc9110#section-15.6.1", "Code group found is not a ValueSet.");
    }

    /// <summary>
    /// Builds the <c>CustomizeProblemDetails</c> callback the service registers at startup, so the
    /// runtime-only behaviour can be exercised without standing up a host or issuing an HTTP request.
    /// </summary>
    private static Action<ProblemDetailsContext> GetConfiguredCustomization()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var options = new ServiceCollection()
            .AddTerminologyProblemDetails(environment.Object)
            .BuildServiceProvider()
            .GetRequiredService<IOptions<ProblemDetailsOptions>>();

        return Assert.IsType<Action<ProblemDetailsContext>>(options.Value.CustomizeProblemDetails);
    }

    private static ProblemDetailsContext BuildContext(int status, string detail) => new()
    {
        HttpContext = new DefaultHttpContext(),
        ProblemDetails = new ProblemDetails { Status = status, Detail = detail }
    };

    [Fact]
    public void ProblemDetailsCustomization_ForServerError_ReplacesRawExceptionDetail()
    {
        // Arrange - the raw message a 500 would otherwise carry out of the controller
        var context = BuildContext(StatusCodes.Status500InternalServerError, "Value set could not be copied.");

        // Act
        GetConfiguredCustomization()(context);

        // Assert - internal state is replaced by a generic message, and a traceId is added to correlate
        Assert.Equal(
            "An error occurred in our API. Please use the trace id when requesting assistance.",
            context.ProblemDetails.Detail);
        Assert.DoesNotContain("Value set could not be copied", context.ProblemDetails.Detail);
        Assert.True(context.ProblemDetails.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public void ProblemDetailsCustomization_ForClientError_PreservesDetail()
    {
        // Arrange - scrubbing must be limited to 5xx; a 4xx detail is actionable and must survive
        var context = BuildContext(StatusCodes.Status404NotFound, "Value set not found with ID missing-vs.");

        // Act
        GetConfiguredCustomization()(context);

        // Assert
        Assert.Equal("Value set not found with ID missing-vs.", context.ProblemDetails.Detail);
        Assert.True(context.ProblemDetails.Extensions.ContainsKey("traceId"));
    }
}
