using Hl7.Fhir.Model;
using LantanaGroup.Link.Terminology.Application.Models;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Amazon.Runtime.Internal;

namespace LantanaGroup.Link.Terminology.Services;

/**
 * Service for FHIR terminology operations. Implements portions of FHIR terminology, as defined in these specifications:
 * https://build.fhir.org/valueset-operation-expand.html
 * https://build.fhir.org/codesystem-operation-validate-code.html
 * https://build.fhir.org/valueset-operation-validate-code.html
 */
public class FhirService(CodeGroupCacheService cacheService, ILogger<FhirService> logger)
{
    public ValueSet GetValueSetById(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("No id parameter specified", nameof(id));

        var codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);

        if (codeGroup == null)
            throw new KeyNotFoundException($"Value set not found with ID {id}");

        return codeGroup.Resource as ValueSet;
    }

    public Bundle GetValueSets(string? url, SummaryType? summary)
    {
        if (string.IsNullOrEmpty(url) && summary == null)
        {
            logger.LogError("No url or summary parameter specified while searching for all value sets (no url specified)");
            throw new ArgumentException("Must specify url if summary is not requested");
        }

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
                    throw new InvalidOperationException("Code group found is not a ValueSet");
                }

                ValueSet clone = (ValueSet)codeGroup.Resource.DeepCopy();

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
                
                bundle.AddResourceEntry(clone, $"/api/fhir/ValueSet/{codeGroup.Id}");
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

                bundle.AddResourceEntry(vs, $"/api/fhir/ValueSet/{codeGroup.Id}");
            }
        }

        bundle.Total = bundle.Entry.Count;
        return bundle;
    }

    public ValueSet ExpandValueSet(string? id, string? url, string? date)
    {
        CodeGroup? codeGroup = null;

        if (!string.IsNullOrEmpty(id))
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);
        else if (!string.IsNullOrEmpty(url))
            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);
        else 
            throw new ArgumentException("No id or url parameter specified");

        if (codeGroup == null)
            throw new KeyNotFoundException($"Value set not found with ID {id}");

        var valueSet = codeGroup.Resource as ValueSet;

        if (valueSet == null)
        {
            logger.LogError("Code group found is not a ValueSet");
            throw new InvalidOperationException("Code group found is not a ValueSet");
        }

        var valueSetCopy = valueSet.DeepCopy() as ValueSet;

        if (valueSetCopy == null)
        {
            logger.LogError("Value set could not be copied");
            throw new InvalidOperationException("Value set could not be copied");
        }

        valueSetCopy.Compose = null;

        foreach (var systemKey in codeGroup.Codes.Keys)
        {
            valueSetCopy.Expansion = new ValueSet.ExpansionComponent();

            foreach (var code in codeGroup.Codes[systemKey])
                valueSetCopy.Expansion.Contains.Add(new ValueSet.ContainsComponent
                {
                    System = systemKey,
                    Code = code.Value,
                    Display = code.Display
                });
        }

        return valueSetCopy;
    }

    public CodeSystem GetCodeSystemById(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("No id parameter specified", nameof(id));

        CodeGroup? codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id);

        if (codeGroup == null)
            throw new KeyNotFoundException($"Code system not found with ID {id}");

        return codeGroup.Resource as CodeSystem;
    }

    public Bundle GetCodeSystems(string url, SummaryType? summary)
    {
        if (string.IsNullOrEmpty(url) && (summary == null))
        {
            logger.LogError("No url or summary parameter specified while searching for all code systems (no url specified)");
            throw new ArgumentException("Must specify url if summary is not requested");
        }

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
                    throw new InvalidOperationException("Code group found is not a CodeSystem");
                }

                CodeSystem clone = (CodeSystem)codeGroup.Resource.DeepCopy();

                if (summary != SummaryType.True)
                {
                    logger.LogDebug("Search performed without summary mode for code system {Url}", url.SanitizeAndRemove());

                    foreach (var codeGroupCode in codeGroup.Codes[codeGroup.Codes.Keys.First()])
                    {
                        clone.Concept.Add(new CodeSystem.ConceptDefinitionComponent()
                        {
                            Code = codeGroupCode.Value,
                            Display = codeGroupCode.Display
                        });
                    }
                }
                
                bundle.AddResourceEntry(clone, $"/api/fhir/CodeSystem/{codeGroup.Id}");
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

                bundle.AddResourceEntry(cs, $"/api/fhir/CodeSystem/{codeGroup.Id}");
            }
        }

        return bundle;
    }

    public Parameters ValidateCodeInCodeSystem(string? url, string? id, string? code, string? display, Parameters? parameters)
    {
        var urlComponent = parameters?.Get("url").FirstOrDefault();
        var codeComponent = parameters?.Get("code").FirstOrDefault();
        var displayComponent = parameters?.Get("display").FirstOrDefault();

        if (urlComponent?.Value != null && string.IsNullOrEmpty(url))
            url = urlComponent.Value.ToString();
        if (codeComponent?.Value != null && string.IsNullOrEmpty(code))
            code = codeComponent.Value.ToString();
        if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
            display = displayComponent.Value.ToString();

        CodeGroup? codeGroup = null;

        if (!string.IsNullOrEmpty(id))
        {
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id);
            url = codeGroup?.Url;
        }
        else if (!string.IsNullOrEmpty(url))
        {
            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, url);
        }
        else
            throw new ArgumentException("No id or url parameter specified");

        if (codeGroup == null)
            return CreateValidationParameters(false, "Code system not found");

        // Priority 1: Direct parameters
        if (!string.IsNullOrEmpty(code))
        {
            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
                display = displayComponent.Value.ToString();
            return ValidateCodeInCodeGroup(codeGroup, code, url, display);
        }

        // Priority 2: Parameters.code and Parameters.system
        if (codeComponent?.Value != null)
        {
            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
                display = displayComponent.Value.ToString();
            return ValidateCodeInCodeGroup(codeGroup, codeComponent.Value.ToString(), url, display);
        }

        // Priority 3: Parameters.coding
        var coding = parameters?.Get("coding").FirstOrDefault()?.Value as Coding;
        if (coding != null)
        {
            return ValidateCodeInCodeGroup(codeGroup, coding.Code, url, coding.Display);
        }

        // Priority 4: Parameters.codeableConcept
        var codeableConcept = parameters?.Get("codeableConcept").FirstOrDefault()?.Value as CodeableConcept;
        if (codeableConcept?.Coding != null)
        {
            foreach (var conceptCoding in codeableConcept.Coding)
            {
                var result = ValidateCodeInCodeGroup(codeGroup, conceptCoding.Code, url, conceptCoding.Display);
                var resultBoolean = result.GetSingleValue<FhirBoolean>("result");
                if (resultBoolean?.Value == true)
                {
                    return result;
                }
            }
        }

        return CreateValidationParameters(false, "No valid code found in parameters");
    }

    public Parameters ValidateCodeInValueSet(string? url, string? id, string? system, string? code, string? display, Parameters? parameters)
    {
        var urlComponent = parameters?.Get("url").FirstOrDefault();
        var systemComponent = parameters?.Get("system").FirstOrDefault();
        var codeComponent = parameters?.Get("code").FirstOrDefault();
        var displayComponent = parameters?.Get("display").FirstOrDefault();

        if (urlComponent?.Value != null && string.IsNullOrEmpty(url))
            url = urlComponent.Value.ToString();

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

        // Priority 1: Direct parameters
        if (!string.IsNullOrEmpty(code))
        {
            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
                display = displayComponent.Value.ToString();
            return ValidateCodeInCodeGroup(codeGroup, code, system, display);
        }

        // Priority 2: Parameters.code and Parameters.system
        if (codeComponent?.Value != null)
        {
            if (systemComponent?.Value != null && string.IsNullOrEmpty(system))
                system = systemComponent.Value.ToString();
            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
                display = displayComponent.Value.ToString();
            return ValidateCodeInCodeGroup(codeGroup, codeComponent.Value.ToString(), system, display);
        }

        // Priority 3: Parameters.coding
        var coding = parameters?.Get("coding").FirstOrDefault()?.Value as Coding;
        if (coding != null)
        {
            return ValidateCodeInCodeGroup(codeGroup, coding.Code, coding.System, coding.Display);
        }

        // Priority 4: Parameters.codeableConcept
        var codeableConcept = parameters?.Get("codeableConcept").FirstOrDefault()?.Value as CodeableConcept;
        if (codeableConcept?.Coding != null)
        {
            foreach (var conceptCoding in codeableConcept.Coding)
            {
                var result = ValidateCodeInCodeGroup(codeGroup, conceptCoding.Code, conceptCoding.System, conceptCoding.Display);
                var resultBoolean = result.GetSingleValue<FhirBoolean>("result");
                if (resultBoolean?.Value == true)
                {
                    return result;
                }
            }
        }

        return CreateValidationParameters(false, "No valid code found in parameters");
    }

    public CapabilityStatement GetMetaData()
    {
        var codeSystemResource = new CapabilityStatement.ResourceComponent()
        {
            Type = "CodeSystem",
            Interaction = new List<CapabilityStatement.ResourceInteractionComponent>()
            {
                new()
                {
                    Code = CapabilityStatement.TypeRestfulInteraction.Read,
                    Documentation = "Read a code system"
                }
            },
            Operation = new AutoConstructedList<CapabilityStatement.OperationComponent>()
            {
                new()
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
                new()
                {
                    Name = "validate-code",
                    Definition = "http://hl7.org/fhir/OperationDefinition/ValueSet-validate-code",
                    Documentation = "Validate a code in a value set"
                },
                new()
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
            {
                matchedCode = codeGroup.Codes[systemKey].Any(c => c.Value == code);
                
                if (matchedCode)
                {
                    var codeObject = codeGroup.Codes[systemKey].First(c => c.Value == code);

                    if (display != null && codeObject.Display == display)
                        matchedDisplay = true;

                    // If we got here, we found a code and can stop looking
                    break;
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

        return CreateValidationParameters(false, $"Code not found in {codeGroup.Type}");
    }
}
