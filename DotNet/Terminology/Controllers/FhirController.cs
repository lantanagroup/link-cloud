using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
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
    public ActionResult<Bundle> GetValueSets([FromQuery] string url,
        [FromQuery(Name = "_summary")] SummaryType? summary)
    {
        try
        {
            return Ok(fhirService.GetValueSets(url.Sanitize(), summary));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
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
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
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
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
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
    public ActionResult<Bundle> GetCodeSystems([FromQuery] string url, [FromQuery(Name = "_summary")] SummaryType? summary)
    {
        try
        {
            return Ok(fhirService.GetCodeSystems(url.Sanitize(), summary));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
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
                url?.Sanitize(), 
                id?.Sanitize(), 
                code?.Sanitize(),
                display?.Sanitize(),
                parameters));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
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
    /// additional information about the validation outcome.
    /// </returns>
    [HttpPost("ValueSet/$validate-code")]
    [HttpPost("ValueSet/{id}/$validate-code")]
    public ActionResult<Parameters> ValidateCodeInValueSet([FromQuery] string? url, [FromRoute] string? id,
        [FromQuery] string? system, [FromQuery] string? code, [FromQuery] string? display,
        [FromBody] Parameters? parameters)
    {
        try
        {
            return Ok(fhirService.ValidateCodeInValueSet(url, id, system, code, display, parameters));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
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