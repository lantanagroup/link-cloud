using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Terminology.Application.Models;
using Terminology.Services;
using Code = Terminology.Application.Models.Code;

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
public class FhirTerminologyController(CodeGroupCacheService cacheService, ILogger<FhirTerminologyController> logger): Controller
{
    [HttpGet("ValueSet/$expand")]
    [HttpGet("ValueSet/{id}/$expand")]
    public ActionResult<ValueSet> ExpandValueSet([FromRoute] string? id, [FromQuery] string? url, [FromQuery] string? date)
    {
        CodeGroup codeGroup = null;

        if (!string.IsNullOrEmpty(id))
        {
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);
        }
        else
        {
            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);
        }

        if (codeGroup == null)
            return NotFound("Value set not found");

        ValueSet? valueSet = codeGroup.Resource as ValueSet;

        if (valueSet == null)
        {
            logger.LogError("Code group found is not a ValueSet");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        ValueSet? valueSetCopy = valueSet.DeepCopy() as ValueSet;

        if (valueSetCopy == null)
        {
            logger.LogError("Value set could not be copied");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        
        valueSet.Compose = null;

        foreach (var systemKey in codeGroup.Codes.Keys)
        {
            valueSet.Expansion = new();

            foreach (var code in codeGroup.Codes[systemKey])
            {
                valueSet.Expansion.Contains.Add(new ValueSet.ContainsComponent()
                {
                    System = systemKey,
                    Code = code.Value,
                    Display = code.Display
                });
            }
        }

        return valueSet;
    }

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
            bool matchedCode = false;
            bool matchedDisplay = false;
            
            foreach (string systemKey in codeGroup.Codes.Keys)
            {
                if (codeGroup.Codes[systemKey].Any(c => c.Value == code))
                {
                    Code codeObject = codeGroup.Codes[systemKey].First(c => c.Value == code);
                    
                    if (display != null && codeObject.Display == display)
                    {
                        matchedDisplay = true;
                    }
                    else
                    {
                        matchedCode = true;
                    }

                    if (matchedCode)
                        continue;
                }
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
    
    [HttpPost("CodeSystem/$validate-code")]
    [HttpPost("CodeSystem/{id}/$validate-code")]
    public Parameters ValidateCodeInCodeSystem([FromQuery] string? url, [FromRoute] string? id, [FromQuery] string? code, [FromQuery] string? display, [FromBody] Parameters? parameters)
    {
        Parameters.ParameterComponent? urlComponent = parameters?.Get("url").FirstOrDefault();
        Parameters.ParameterComponent? codeComponent = parameters?.Get("code").FirstOrDefault();
        Parameters.ParameterComponent? displayComponent = parameters?.Get("display").FirstOrDefault();

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
    public Parameters ValidateCodeInValueSet([FromQuery] string? url, [FromRoute] string? id, [FromQuery] string? system, [FromQuery] string? code, [FromQuery] string? display, [FromBody] Parameters? parameters)
    {
        Parameters.ParameterComponent? urlComponent = parameters?.Get("url").FirstOrDefault();
        Parameters.ParameterComponent? systemComponent = parameters?.Get("system").FirstOrDefault();
        Parameters.ParameterComponent? codeComponent = parameters?.Get("code").FirstOrDefault();
        Parameters.ParameterComponent? displayComponent = parameters?.Get("display").FirstOrDefault();

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
    
    private static Parameters CreateValidationParameters(bool result, string? message = null)
    {
        var parameters = new Parameters();
        parameters.Add("result", new FhirBoolean(result));
        if (message != null)
            parameters.Add("message", new FhirString(message));
        return parameters;
    }
}