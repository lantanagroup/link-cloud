using Amazon.Runtime.Internal;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LantanaGroup.Link.Terminology.Controllers;

/**
 * Controller for FHIR terminology operations. Implements portions of FHIR terminology, as defined in these specifications:
 * https://build.fhir.org/valueset-operation-expand.html
 * https://build.fhir.org/codesystem-operation-validate-code.html
 * https://build.fhir.org/valueset-operation-validate-code.html
 * The class uses the CodeGroupCacheService to retrieve code groups, depending on if the operation relates to a ValueSet
 * or a CodeSystem. It uses the cached CodeGroup to validate codes in value sets and code systems, as well as expand value sets
 * to the enumerated list of codes that were provided to the terminology service. It does not *actually* perform
 * expansion of value sets, and only returns the pre-expanded/enumerated codes.
 */
[Route("api/terminology/fhir")]
[SwaggerTag("FHIR Terminology Operations")]
public class FhirController(CodeGroupCacheService cacheService, ILogger<FhirController> logger): Controller
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
        if (string.IsNullOrEmpty(id))
            return BadRequest("No id parameter specified");

        var codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);

        if (codeGroup == null)
            return NotFound($"Value set not found with ID {id}");

        return Ok(codeGroup.Resource as ValueSet);
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
        if (string.IsNullOrEmpty(url) && summary == null)
        {
            logger.LogError("No url or summary parameter specified while searching for all value sets (no url specified)");
            return BadRequest("Must specify url if summary is not requested");
        }
        
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset
        };

        if (!string.IsNullOrEmpty(url))
        {
            var codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);

            if (codeGroup != null)
            {
                if (codeGroup.Resource is not ValueSet)
                {
                    logger.LogError("Code group found is not a ValueSet");
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
                
                ValueSet clone = (ValueSet)codeGroup.Resource.DeepCopy();
                bundle.AddResourceEntry(clone, baseUrl + "/api/fhir/ValueSet/" + codeGroup.Id);

                // If not summary mode, then enumerate each code in the value set as part of the the expansion.contains property
                if (summary != SummaryType.True)
                {
                    clone.Expansion = new ValueSet.ExpansionComponent();
                    
                    foreach (var codeGroupSystem in codeGroup.Codes)
                    {
                        ValueSet.ContainsComponent contains = new ValueSet.ContainsComponent()
                        {
                            System = codeGroupSystem.Key
                        };
                        
                        clone.Expansion.Contains.Add(contains);
                        
                        foreach (var codeGroupCode in codeGroupSystem.Value)
                        {
                            contains.Contains.Add(new ValueSet.ContainsComponent()
                            {
                                Code = codeGroupCode.Value,
                                Display = codeGroupCode.Display
                            });
                        }
                    }
                }
            }
        }
        else
        {
            var codeGroups = cacheService.GetAllCodeGroups(CodeGroup.CodeGroupTypes.ValueSet);

            foreach (var codeGroup in codeGroups)
            {
                var vs = new ValueSet
                {
                    Id = codeGroup.Id,
                    Url = codeGroup.Url,
                    Version = codeGroup.Version,
                    Name = codeGroup.Name
                };

                bundle.AddResourceEntry(vs, baseUrl + "/api/fhir/ValueSet/" + codeGroup.Id);
            }
        }

        return Ok(bundle);
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
        CodeGroup? codeGroup = null;

        if (!string.IsNullOrEmpty(id))
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);
        else if (!string.IsNullOrEmpty(url))
            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);
        else 
            return BadRequest("No id or url parameter specified");

        if (codeGroup == null)
            return NotFound($"Value set not found with ID {id}");

        var valueSet = codeGroup.Resource as ValueSet;

        if (valueSet == null)
        {
            logger.LogError("Code group found is not a ValueSet");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        var valueSetCopy = valueSet.DeepCopy() as ValueSet;

        if (valueSetCopy == null)
        {
            logger.LogError("Value set could not be copied");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        valueSet.Compose = null;

        foreach (var systemKey in codeGroup.Codes.Keys)
        {
            valueSet.Expansion = new ValueSet.ExpansionComponent();

            foreach (var code in codeGroup.Codes[systemKey])
                valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent
                {
                    System = systemKey,
                    Code = code.Value,
                    Display = code.Display
                });
        }

        return valueSet;
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
    public ActionResult<ValueSet> GetCodeSystemById([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("No id parameter specified");

        CodeGroup? codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id);
        
        if (codeGroup == null)
            return NotFound($"Code system not found with ID {id}");

        return Ok(codeGroup.Resource as CodeSystem);
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
        if (string.IsNullOrEmpty(url) && (summary == null))
        {
            logger.LogError("No url or summary parameter specified while searching for all code systems (no url specified)");
            return BadRequest("Must specify url if summary is not requested");
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        Bundle bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset
        };

        if (!string.IsNullOrEmpty(url))
        {
            CodeGroup? codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, url);

            if (codeGroup != null)
            {
                if (codeGroup.Resource is not CodeSystem)
                {
                    logger.LogError("Code group found is not a CodeSystem");
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
                
                CodeSystem clone = (CodeSystem)codeGroup.Resource.DeepCopy();
                bundle.AddResourceEntry(clone, baseUrl + "/api/fhir/CodeSystem/" + codeGroup.Id);

                if (summary != SummaryType.True)
                {
                    logger.LogDebug($"Search performed without summary mode for code system {url}");
                    
                    foreach (var codeGroupCode in codeGroup.Codes[codeGroup.Codes.Keys.First()])
                    {
                        clone.Concept.Add(new CodeSystem.ConceptDefinitionComponent()
                        {
                            Code = codeGroupCode.Value,
                            Display = codeGroupCode.Display
                        });
                    }
                }
            }
        }
        else
        {
            List<CodeGroup> codeGroups = cacheService.GetAllCodeGroups(CodeGroup.CodeGroupTypes.CodeSystem);

            foreach (var codeGroup in codeGroups)
            {
                CodeSystem cs = new CodeSystem
                {
                    Id = codeGroup.Id,
                    Url = codeGroup.Url,
                    Version = codeGroup.Version,
                    Name = codeGroup.Name
                };
                
                bundle.AddResourceEntry(cs, baseUrl + "/api/fhir/CodeSystem/" + codeGroup.Id);
            }
        }
                
        return Ok(bundle);
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
        var urlComponent = parameters?.Get("url").FirstOrDefault();
        var codeComponent = parameters?.Get("code").FirstOrDefault();
        var displayComponent = parameters?.Get("display").FirstOrDefault();

        if (urlComponent != null && urlComponent.Value != null && string.IsNullOrEmpty(url))
            url = urlComponent.Value.ToString();
        if (codeComponent != null && codeComponent.Value != null && string.IsNullOrEmpty(code))
            code = codeComponent.Value.ToString();
        if (displayComponent != null && displayComponent.Value != null && string.IsNullOrEmpty(display))
            display = displayComponent.Value.ToString();

        if (code == null)
            return CreateValidationParameters(false, "code parameter is required");

        CodeGroup? codeGroup = null;

        if (!string.IsNullOrEmpty(id))
        {
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id);
            url = codeGroup?.Url;
        }
        else if (!string.IsNullOrEmpty(url))
        {
            if (url == null)
                return Ok(CreateValidationParameters(false, "url parameter is required"));

            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, url);
        }
        else
            return BadRequest("No id or url parameter specified");

        if (codeGroup == null)
            return Ok(CreateValidationParameters(false, "Code system not found"));

        return Ok(ValidateCodeInCodeGroup(codeGroup, code, url, display));
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
    public Parameters ValidateCodeInValueSet([FromQuery] string? url, [FromRoute] string? id,
        [FromQuery] string? system, [FromQuery] string? code, [FromQuery] string? display,
        [FromBody] Parameters? parameters)
    {
        var urlComponent = parameters?.Get("url").FirstOrDefault();
        var systemComponent = parameters?.Get("system").FirstOrDefault();
        var codeComponent = parameters?.Get("code").FirstOrDefault();
        var displayComponent = parameters?.Get("display").FirstOrDefault();

        if (urlComponent != null && urlComponent.Value != null && string.IsNullOrEmpty(url))
            url = urlComponent.Value.ToString();
        if (systemComponent != null && systemComponent.Value != null && string.IsNullOrEmpty(system))
            system = systemComponent.Value.ToString();
        if (codeComponent != null && codeComponent.Value != null && string.IsNullOrEmpty(code))
            code = codeComponent.Value.ToString();
        if (displayComponent != null && displayComponent.Value != null && string.IsNullOrEmpty(display))
            display = displayComponent.Value.ToString();

        if (code == null)
            return CreateValidationParameters(false, "code parameter is required");

        CodeGroup? codeGroup = null;

        if (!string.IsNullOrEmpty(id))
        {
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);
            url = codeGroup?.Url;
        }
        else
        {
            if (url == null)
                return CreateValidationParameters(false, "url parameter is required");

            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);
        }

        if (codeGroup == null)
            return CreateValidationParameters(false, "Value set not found");

        return ValidateCodeInCodeGroup(codeGroup, code, system, display);
    }
    
    #endregion

    private Parameters ValidateCodeInCodeGroup(CodeGroup codeGroup, string code, string? system, string? display)
    {
        if (!string.IsNullOrEmpty(system))
        {
            if (!codeGroup.Codes.ContainsKey(system))
                return CreateValidationParameters(false, $"Code system not found in {codeGroup.Type}");

            if (codeGroup.Codes[system].Any(c => c.Value == code))
            {
                if (display != null && !codeGroup.Codes[system].Any(c => c.Value == code && c.Display == display))
                    return CreateValidationParameters(false, "Display does not match code");

                return CreateValidationParameters(true);
            }
        }
        else
        {
            var matchedCode = false;
            var matchedDisplay = false;

            foreach (var systemKey in codeGroup.Codes.Keys)
                if (codeGroup.Codes[systemKey].Any(c => c.Value == code))
                {
                    var codeObject = codeGroup.Codes[systemKey].First(c => c.Value == code);

                    if (display != null && codeObject.Display == display)
                        matchedDisplay = true;
                    else
                        matchedCode = true;

                    if (matchedCode)
                        continue;
                }

            if (matchedCode)
            {
                if (!string.IsNullOrEmpty(display) && !matchedDisplay)
                    return CreateValidationParameters(false, "Display does not match code");

                return CreateValidationParameters(true);
            }

            return CreateValidationParameters(false);
        }

        return CreateValidationParameters(false, "Code not found in code system");
    }

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
        var codeSystemResource = new CapabilityStatement.ResourceComponent()
        {
            Type = "CodeSystem",
            Interaction = new List<CapabilityStatement.ResourceInteractionComponent>()
            {
                new CapabilityStatement.ResourceInteractionComponent()
                {
                    Code = CapabilityStatement.TypeRestfulInteraction.Read,
                    Documentation = "Read a code system"
                }
            },
            Operation = new AutoConstructedList<CapabilityStatement.OperationComponent>()
            {
                new CapabilityStatement.OperationComponent()
                {
                    Name = "validate-code",
                    Definition = "http://hl7.org/fhir/OperationDefinition/CodeSystem-validate-code",
                    Documentation = "Validate a code in a code system"
                }
            }
        };
        var valueSetResource = new CapabilityStatement.ResourceComponent()
        {
            Type = "ValueSet",
            Interaction =
            [
                new CapabilityStatement.ResourceInteractionComponent()
                {
                    Code = CapabilityStatement.TypeRestfulInteraction.Read,
                    Documentation = "Read a value set"
                }
            ],
            Operation = new AutoConstructedList<CapabilityStatement.OperationComponent>()
            {
                new CapabilityStatement.OperationComponent()
                {
                    Name = "validate-code",
                    Definition = "http://hl7.org/fhir/OperationDefinition/ValueSet-validate-code",
                    Documentation = "Validate a code in a value set"
                },
                new CapabilityStatement.OperationComponent()
                {
                    Name = "expand",
                    Definition = "http://hl7.org/fhir/OperationDefinition/ValueSet-expand",
                    Documentation = "Expands a value set using the codes cached in memory"
                }
            }
        };
        
        return new CapabilityStatement()
        {
            Id = "link-tx-service",
            Version = "1.0.0",          // TODO: Replace with assembly/package version
            Name = "Link Terminology Service",
            Title = "Link Terminology Service",
            Status = PublicationStatus.Active,
            DateElement = FhirDateTime.Now(),
            Instantiates = new List<string>() { "http://hl7.org/fhir/CapabilityStatement/terminology-server", "http://hl7.org/fhir/CapabilityStatement/terminology-server-example" },
            Software = new CapabilityStatement.SoftwareComponent()
            {
                Name = "Link",
                Version = "1.0.0"       // TODO: Replace with product/business version
            },
            Format = new List<string>() { "application/fhir+json" },
            Rest =
            [
                new CapabilityStatement.RestComponent()
                {
                    Mode = CapabilityStatement.RestfulCapabilityMode.Server,
                    Security = new CapabilityStatement.SecurityComponent()
                    {
                        Cors = true
                    },
                    Resource =
                    [
                        codeSystemResource,
                        valueSetResource
                    ]
                }
            ]
        };
    }

    private static Parameters CreateValidationParameters(bool result, string? message = null)
    {
        var parameters = new Parameters();
        parameters.Add("result", new FhirBoolean(result));
        if (message != null)
            parameters.Add("message", new FhirString(message));
        return parameters;
    }
}