using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Mvc;
using Terminology.Application.Models;
using Terminology.Services;

namespace Terminology.Controllers;

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
[Route("api/fhir")]
public class FhirController(CodeGroupCacheService cacheService, ILogger<FhirController> logger): Controller
{
    #region Value Sets
    
    [HttpGet("ValueSet/{id}")]
    public ActionResult<ValueSet> GetValueSetById([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("No id parameter specified");

        var codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);

        if (codeGroup == null)
            return NotFound("Value set not found");

        return Ok(codeGroup.Resource as ValueSet);
    }

    [HttpGet("ValueSet")]
    public ActionResult<Bundle> GetValueSets([FromQuery] string url,
        [FromQuery(Name = "_summary")] SummaryType? summary)
    {
        if (string.IsNullOrEmpty(url) && summary == null)
            return BadRequest("Must specify url if summary is not requested");
        
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        if (!string.IsNullOrEmpty(url))
        {
            var codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);

            var bundle = new Bundle
            {
                Type = Bundle.BundleType.Searchset
            };

            if (codeGroup != null)
                bundle.AddResourceEntry(codeGroup.Resource as ValueSet, baseUrl + "/api/fhir/ValueSet/" + codeGroup.Id);

            return Ok(bundle);
        }
        else
        {
            var codeGroups = cacheService.GetAllCodeGroups(CodeGroup.CodeGroupTypes.ValueSet);
            var bundle = new Bundle();
            bundle.Type = Bundle.BundleType.Searchset;

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

            return bundle;
        }
    }

    [HttpGet("ValueSet/$expand")]
    [HttpGet("ValueSet/{id}/$expand")]
    public ActionResult<ValueSet> ExpandValueSet([FromRoute] string? id, [FromQuery] string? url,
        [FromQuery] string? date)
    {
        CodeGroup codeGroup = null;

        if (!string.IsNullOrEmpty(id))
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);
        else
            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);

        if (codeGroup == null)
            return NotFound("Value set not found");

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
    
    [HttpGet("CodeSystem/{id}")]
    public ActionResult<ValueSet> GetCodeSystemById([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("No id parameter specified");

        CodeGroup? codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id);
        
        if (codeGroup == null)
            return NotFound("Code system not found");

        return Ok(codeGroup.Resource as CodeSystem);
    }
    
    [HttpGet("CodeSystem")]
    public ActionResult<Bundle> GetCodeSystems([FromQuery] string url, [FromQuery(Name = "_summary")] SummaryType? summary)
    {
        if (string.IsNullOrEmpty(url) && (summary == null))
            return BadRequest("Must specify url if summary is not requested");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        if (!string.IsNullOrEmpty(url))
        {
            CodeGroup? codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, url);

            Bundle bundle = new Bundle
            {
                Type = Bundle.BundleType.Searchset
            };

            if (codeGroup != null)
                bundle.AddResourceEntry(codeGroup.Resource as CodeSystem, baseUrl + "/api/fhir/CodeSystem/" + codeGroup.Id);

            return Ok(bundle);
        }
        else
        {
            List<CodeGroup> codeGroups = cacheService.GetAllCodeGroups(CodeGroup.CodeGroupTypes.CodeSystem);
            Bundle bundle = new Bundle();
            bundle.Type = Bundle.BundleType.Searchset;

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

            return bundle;
        }
    }

    [HttpPost("CodeSystem/$validate-code")]
    [HttpPost("CodeSystem/{id}/$validate-code")]
    public Parameters ValidateCodeInCodeSystem([FromQuery] string? url, [FromRoute] string? id,
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

        CodeGroup codeGroup = null;

        if (!string.IsNullOrEmpty(id))
        {
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id);
            url = codeGroup?.Url;
        }
        else
        {
            if (url == null)
                return CreateValidationParameters(false, "url parameter is required");

            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, url);
        }

        if (codeGroup == null)
            return CreateValidationParameters(false, "Code system not found");

        return ValidateCodeInCodeGroup(codeGroup, code, url, display);
    }

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

        CodeGroup codeGroup = null;

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

    private static Parameters CreateValidationParameters(bool result, string? message = null)
    {
        var parameters = new Parameters();
        parameters.Add("result", new FhirBoolean(result));
        if (message != null)
            parameters.Add("message", new FhirString(message));
        return parameters;
    }
}