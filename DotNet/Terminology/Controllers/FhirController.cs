using System.Net;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Terminology.Application.Formatters;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LantanaGroup.Link.Terminology.Controllers;

/**
 * Controller for FHIR terminology operations. Implements portions of FHIR terminology, as defined in these specifications:
 * https://build.fhir.org/valueset-operation-expand.html
 * https://build.fhir.org/codesystem-operation-validate-code.html
 * https://build.fhir.org/valueset-operation-validate-code.html
 * The class uses FhirService to handle all FHIR-related operations, which internally uses CodeGroupCacheService 
 * to retrieve and validate codes in value sets and code systems, as well as expand value sets.
 */
[Route("api/terminology/fhir")]
[SwaggerTag("FHIR Terminology Operations")]
[ApiController]
public class FhirController(FhirService fhirService) : Controller
{
    /// <summary>
    /// Sanitizes an untrusted terminology value (url, system, code or display) that is compared
    /// against loaded terminology content rather than rendered as markup.
    /// </summary>
    /// <remarks>
    /// <see cref="HtmlInputSanitizer.Sanitize"/> strips markup but also HTML-encodes the surviving text,
    /// which corrupts values that legitimately contain reserved characters — over 2,000 LOINC displays
    /// contain "&amp;", and encoding one to "&amp;amp;" turns a correct display into a spurious
    /// "Display does not match code". Decoding afterwards restores the plain text while still dropping
    /// any markup the sanitizer removed.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when a non-empty value sanitizes away to nothing, i.e. the caller supplied markup and no
    /// content. The emptied value must not be passed on: an empty display disables the display check
    /// entirely, so the request would be answered with result=true instead of being rejected.
    /// </exception>
    private static string? SanitizeTerminologyValue(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        var sanitized = WebUtility.HtmlDecode(value.Sanitize());

        if (value.Length > 0 && sanitized.Length == 0)
        {
            throw new ArgumentException($"Invalid value supplied for '{parameterName}'");
        }

        return sanitized;
    }

    /// <summary>
    /// Builds an RFC 9457 Problem Details result for a failed terminology request, matching the shape
    /// <see cref="ConfigController"/> already returns. The <c>traceId</c> extension is added by the
    /// service-wide customization in <c>TerminologyProblemDetailsExtensions</c>.
    /// </summary>
    /// <remarks>
    /// Exception messages are written as fragments ("Value set not found with ID x") because they are
    /// also read from logs and asserted in unit tests. <c>detail</c> is prose, so a terminating period
    /// is added here rather than baked into every message at the throw site.
    /// </remarks>
    private ObjectResult TerminologyProblem(HttpStatusCode statusCode, string title, string type, string detail)
    {
        var sentence = detail.EndsWith('.') || detail.EndsWith('?') || detail.EndsWith('!')
            ? detail
            : detail + ".";

        return Problem(detail: sentence, statusCode: (int)statusCode, title: title, type: type);
    }

    /// <summary>Client input failed validation. RFC 9110 section 15.5.1.</summary>
    private ObjectResult BadRequestProblem(string detail) => TerminologyProblem(
        HttpStatusCode.BadRequest, "Bad Request", "https://tools.ietf.org/html/rfc9110#section-15.5.1", detail);

    /// <summary>The requested terminology resource is not loaded. RFC 9110 section 15.5.5.</summary>
    private ObjectResult NotFoundProblem(string detail) => TerminologyProblem(
        HttpStatusCode.NotFound, "Not Found", "https://tools.ietf.org/html/rfc9110#section-15.5.5", detail);

    /// <summary>
    /// A loaded code group could not be used as requested. RFC 9110 section 15.6.1. The customization
    /// replaces <c>detail</c> with a generic message so internal state is not exposed to the caller.
    /// </summary>
    private ObjectResult InternalServerErrorProblem(string detail) => TerminologyProblem(
        HttpStatusCode.InternalServerError, "Internal Server Error", "https://tools.ietf.org/html/rfc9110#section-15.6.1", detail);

    #region Value Sets

    /// <summary>
    /// Retrieves a ValueSet resource by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the ValueSet resource to retrieve.</param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing the requested <see cref="ValueSet"/>
    /// if it exists, a 400 Bad Request response if the id is null or empty, or a
    /// 404 Not Found response if the ValueSet is not found.
    /// </returns>
    [HttpGet("ValueSet/{id}")]
    public ActionResult<ValueSet> GetValueSetById([FromRoute] string id)
    {
        try
        {
            var cleanId = id.SanitizeAndRemove();
            return Ok(fhirService.GetValueSetById(cleanId));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves a collection of ValueSet resources based on the specified query parameters.
    /// </summary>
    /// <param name="url">The canonical URL of the ValueSet to retrieve, if specified.</param>
    /// <param name="summary">
    /// An optional parameter indicating if a summary of the ValueSet should be included in the response.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing a <see cref="Bundle"/> with the requested
    /// ValueSet resources. Returns a 400 Bad Request response if neither <paramref name="url"/>
    /// nor <paramref name="summary"/> are provided.
    /// </returns>
    [HttpGet("ValueSet")]
    public ActionResult<Bundle> GetValueSets([FromQuery] string? url,
        [FromQuery(Name = "_summary")] SummaryType? summary)
    {
        try
        {
            return Ok(fhirService.GetValueSets(url?.Sanitize(), summary));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return InternalServerErrorProblem(ex.Message);
        }
    }

    /// <summary>
    /// Expands a ValueSet resource by its unique identifier or URL.
    /// </summary>
    /// <param name="id">The unique identifier of the ValueSet resource to expand. Can be null if URL is provided.</param>
    /// <param name="url">The URL of the ValueSet resource to expand. Can be null if id is provided.</param>
    /// <param name="date">The date to use when querying the ValueSet resource. Optional.</param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing the expanded <see cref="ValueSet"/>
    /// if it exists, or appropriate error responses such as 404 Not Found or 500 Internal Server Error.
    /// </returns>
    [HttpGet("ValueSet/$expand")]
    [HttpGet("ValueSet/{id}/$expand")]
    public ActionResult<ValueSet> ExpandValueSet([FromRoute] string? id, [FromQuery] string? url,
        [FromQuery] string? date)
    {
        try
        {
            return Ok(fhirService.ExpandValueSet(id?.Sanitize(), url?.Sanitize(), date?.Sanitize()));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return InternalServerErrorProblem(ex.Message);
        }
    }

    #endregion

    #region Code Systems

    /// <summary>
    /// Retrieves a CodeSystem resource by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the CodeSystem resource to retrieve.</param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing the requested <see cref="CodeSystem"/>
    /// if it exists, a 400 Bad Request response if the id is null or empty, or a
    /// 404 Not Found response if the CodeSystem is not found.
    /// </returns>
    [HttpGet("CodeSystem/{id}")]
    public ActionResult<CodeSystem> GetCodeSystemById([FromRoute] string id)
    {
        try
        {
            return Ok(fhirService.GetCodeSystemById(id));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundProblem(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves a collection of CodeSystem resources based on the specified query parameters.
    /// </summary>
    /// <param name="url">The canonical URL of the CodeSystem to retrieve. If provided, retrieves a specific CodeSystem.</param>
    /// <param name="summary">
    /// An optional parameter to request a summary representation of the CodeSystems.
    /// If not specified, full details will be retrieved.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing a <see cref="Bundle"/> with the requested
    /// CodeSystem resources. Returns a 400 Bad Request response if neither the <paramref name="url"/>
    /// nor <paramref name="summary"/> is provided.
    /// </returns>
    [HttpGet("CodeSystem")]
    public ActionResult<Bundle> GetCodeSystems([FromQuery] string? url, [FromQuery(Name = "_summary")] SummaryType? summary)
    {
        try
        {
            return Ok(fhirService.GetCodeSystems(url?.Sanitize(), summary));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return InternalServerErrorProblem(ex.Message);
        }
    }

    /// <summary>
    /// Validates a code in a specific CodeSystem, using either the CodeSystem's unique identifier
    /// or its URL. Optionally validates its display value as well.
    /// </summary>
    /// <param name="url">The URL of the CodeSystem in which the code should be validated. Optional if the id is provided.</param>
    /// <param name="id">The unique identifier of the CodeSystem. Optional if the URL is provided.</param>
    /// <param name="code">The code to validate. This parameter is required.</param>
    /// <param name="display">An optional display value to validate against the code.</param>
    /// <param name="parameters">A set of parameters containing validation details, such as the URL, code, and display, if not passed via other parameters.</param>
    /// <returns>
    /// A <see cref="Parameters"/> resource indicating the validation result. This includes a boolean "result"
    /// indicating success or failure, and a message explaining the result if applicable.
    /// </returns>
    [HttpPost("CodeSystem/$validate-code")]
    [HttpPost("CodeSystem/{id}/$validate-code")]
    public ActionResult<Parameters> ValidateCodeInCodeSystem([FromQuery] string? url, [FromRoute] string? id,
        [FromQuery] string? code, [FromQuery] string? display, [FromBody] Parameters? parameters)
    {
        try
        {
            return Ok(fhirService.ValidateCodeInCodeSystem(
                SanitizeTerminologyValue(url, nameof(url)),
                SanitizeTerminologyValue(id, nameof(id)),
                SanitizeTerminologyValue(code, nameof(code)),
                SanitizeTerminologyValue(display, nameof(display)),
                parameters));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    /// <summary>
    /// Looks up details for a code in a specific CodeSystem using query parameters.
    /// </summary>
    [HttpGet("CodeSystem/$lookup")]
    [HttpGet("CodeSystem/{id}/$lookup")]
    public ActionResult<Parameters> LookupCodeInCodeSystem([FromQuery] string? system, [FromRoute] string? id,
        [FromQuery] string? code, [FromQuery] string? version)
    {
        try
        {
            return Ok(fhirService.LookupCodeInCodeSystem(
                null,
                id?.Sanitize(),
                system?.Sanitize(),
                code?.Sanitize(),
                version?.Sanitize(),
                null));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Looks up details for a code in a specific CodeSystem using query/body parameters.
    /// </summary>
    [HttpPost("CodeSystem/$lookup")]
    [HttpPost("CodeSystem/{id}/$lookup")]
    [ValidateAntiForgeryToken]
    public ActionResult<Parameters> LookupCodeInCodeSystem([FromQuery] string? system, [FromRoute] string? id,
        [FromQuery] string? code, [FromQuery] string? version, [FromBody] Parameters? parameters)
    {
        try
        {
            var sanitizedParameters = SanitizeLookupCodeParameters(parameters);

            return Ok(fhirService.LookupCodeInCodeSystem(
                null,
                id?.Sanitize(),
                system?.Sanitize(),
                code?.Sanitize(),
                version?.Sanitize(),
                sanitizedParameters));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private static Parameters? SanitizeLookupCodeParameters(Parameters? parameters)
    {
        if (parameters == null)
        {
            return null;
        }

        foreach (var parameter in parameters.Parameter)
        {
            if (parameter.Name is "code" or "system" or "version")
            {
                var sanitizedValue = parameter.Value?.ToString()?.Sanitize();
                parameter.Value = sanitizedValue == null ? null : new FhirString(sanitizedValue);
                continue;
            }

            if (parameter.Name == "coding" && parameter.Value is Coding coding)
            {
                coding.Code = coding.Code?.Sanitize();
                coding.System = coding.System?.Sanitize();
                coding.Version = coding.Version?.Sanitize();
            }
        }

        return parameters;
    }

    /// <summary>
    /// Validates a given code, optionally with its system and display, against a specified ValueSet.
    /// </summary>
    /// <param name="url">The canonical URL of the ValueSet to validate against. This parameter is optional if the id is provided.</param>
    /// <param name="id">The unique identifier of the ValueSet to validate against. This parameter is optional if the URL is provided.</param>
    /// <param name="system">The system of the code to validate. This parameter is optional.</param>
    /// <param name="code">The code to validate. This parameter is required.</param>
    /// <param name="display">The display text associated with the code to validate. This parameter is optional.</param>
    /// <param name="parameters">Additional parameters supplied in the request body to guide the validation operation. This parameter is optional.</param>
    /// <returns>
    /// A <see cref="Parameters"/> resource that indicates the result of the validation and may contain
    /// additional information about the validation outcome, or a 400 Bad Request response when neither
    /// <paramref name="url"/> nor <paramref name="id"/> identifies a ValueSet.
    /// </returns>
    [HttpPost("ValueSet/$validate-code")]
    [HttpPost("ValueSet/{id}/$validate-code")]
    public ActionResult<Parameters> ValidateCodeInValueSet([FromQuery] string? url, [FromRoute] string? id,
        [FromQuery][PreserveEmptyString] string? system, [FromQuery] string? code, [FromQuery] string? display,
        [FromBody] Parameters? parameters)
    {
        try
        {
            return Ok(fhirService.ValidateCodeInValueSet(
                SanitizeTerminologyValue(url, nameof(url)),
                SanitizeTerminologyValue(id, nameof(id)),
                SanitizeTerminologyValue(system, nameof(system)),
                SanitizeTerminologyValue(code, nameof(code)),
                SanitizeTerminologyValue(display, nameof(display)),
                parameters));
        }
        catch (ArgumentException ex)
        {
            return BadRequestProblem(ex.Message);
        }
    }

    #endregion

    /// <summary>
    /// Returns the CapabilityStatement describing the functionalities
    /// and conformance requirements of the FHIR Terminology server.
    /// </summary>
    /// <returns>
    /// A <see cref="CapabilityStatement"/> object detailing the supported
    /// capabilities, interactions, formats, and resources of the server.
    /// </returns>
    [HttpGet("metadata")]
    public CapabilityStatement GetMetaData()
    {
        return fhirService.GetMetaData();
    }
}