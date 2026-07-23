using Amazon.Runtime.Internal;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;

namespace LantanaGroup.Link.Terminology.Services;

/**
 * Service for FHIR terminology operations. Implements portions of FHIR terminology, as defined in these specifications:
 * https://build.fhir.org/valueset-operation-expand.html
 * https://build.fhir.org/codesystem-operation-validate-code.html
 * https://build.fhir.org/valueset-operation-validate-code.html
 */
public class FhirService(ICodeGroupCacheService cacheService, ILogger<FhirService> logger)
{
    public ValueSet GetValueSetById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("No id parameter specified", nameof(id));
        }

        var codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);

        if (codeGroup == null)
        {
            throw new KeyNotFoundException($"Value set not found with ID {id}");
        }

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
        {
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);
        }
        else if (!string.IsNullOrEmpty(url))
        {
            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);
        }
        else
        {
            throw new ArgumentException("No id or url parameter specified");
        }

        if (codeGroup == null)
        {
            var lookup = !string.IsNullOrEmpty(id) ? $"ID {id}" : $"URL {url}";
            throw new KeyNotFoundException($"Value set not found with {lookup}");
        }

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

        valueSetCopy.Expansion = new ValueSet.ExpansionComponent();

        foreach (var systemKey in codeGroup.Codes.Keys)
        {
            foreach (var code in codeGroup.Codes[systemKey])
            {
                valueSetCopy.Expansion.Contains.Add(new ValueSet.ContainsComponent
                {
                    System = systemKey,
                    Code = code.Value,
                    Display = code.Display
                });
            }
        }

        return valueSetCopy;
    }

    public CodeSystem GetCodeSystemById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("No id parameter specified", nameof(id));
        }

        CodeGroup? codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id);

        if (codeGroup == null)
        {
            throw new KeyNotFoundException($"Code system not found with ID {id}");
        }

        return codeGroup.Resource as CodeSystem;
    }

    public Bundle GetCodeSystems(string? url, SummaryType? summary)
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

        bundle.Total = bundle.Entry.Count;
        return bundle;
    }

    public Parameters ValidateCodeInCodeSystem(string? url, string? id, string? code, string? display, Parameters? parameters)
    {
        var urlComponent = parameters?.Get("url").FirstOrDefault();
        var codeComponent = parameters?.Get("code").FirstOrDefault();
        var displayComponent = parameters?.Get("display").FirstOrDefault();

        if (urlComponent?.Value != null && string.IsNullOrEmpty(url))
        {
            url = urlComponent.Value.ToString();
        }

        if (codeComponent?.Value != null && string.IsNullOrEmpty(code))
        {
            code = codeComponent.Value.ToString();
        }

        if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
        {
            display = displayComponent.Value.ToString();
        }

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
        {
            throw new ArgumentException("No id or url parameter specified");
        }

        if (codeGroup == null)
        {
            return CreateValidationParameters(false, "Code system not found");
        }

        // Priority 1: Direct parameters
        if (!string.IsNullOrEmpty(code))
        {
            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
            {
                display = displayComponent.Value.ToString();
            }

            return ValidateCodeInCodeGroup(codeGroup, code, url, display);
        }

        // Priority 2: Parameters.code and Parameters.system
        if (codeComponent?.Value != null)
        {
            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
            {
                display = displayComponent.Value.ToString();
            }

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
        {
            url = urlComponent.Value.ToString();
        }

        CodeGroup? codeGroup = null;

        if (!string.IsNullOrEmpty(id))
        {
            codeGroup = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.ValueSet, id);
            url = codeGroup?.Url;
        }
        else
        {
            if (url == null)
            {
                return CreateValidationParameters(false, "url parameter is required");
            }

            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);
        }

        if (codeGroup == null)
        {
            return CreateValidationParameters(false, "Value set not found");
        }

        // Priority 1: Direct parameters
        if (!string.IsNullOrEmpty(code))
        {
            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
            {
                display = displayComponent.Value.ToString();
            }

            return ValidateCodeInCodeGroup(codeGroup, code, system, display);
        }

        // Priority 2: Parameters.code and Parameters.system
        if (codeComponent?.Value != null)
        {
            if (systemComponent?.Value != null && string.IsNullOrEmpty(system))
            {
                system = systemComponent.Value.ToString();
            }

            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
            {
                display = displayComponent.Value.ToString();
            }

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

    public Parameters LookupCodeInCodeSystem(string? url, string? id, string? system, string? code, string? version, Parameters? parameters)
    {
        var codeComponent = parameters?.Get("code").FirstOrDefault()?.Value?.ToString();
        var systemComponent = parameters?.Get("system").FirstOrDefault()?.Value?.ToString();
        var versionComponent = parameters?.Get("version").FirstOrDefault()?.Value?.ToString();
        var coding = parameters?.Get("coding").FirstOrDefault()?.Value as Coding;

        code ??= codeComponent;
        system ??= systemComponent;
        version ??= versionComponent;

        var hasCodeAndSystem = !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(system);
        var hasCoding = coding != null;

        if (hasCodeAndSystem == hasCoding)
        {
            throw new ArgumentException("Specify either code+system, or coding (in Parameters), but not both");
        }

        string codeToLookup;
        string systemIdentifier;

        if (hasCoding)
        {
            codeToLookup = coding!.Code;
            systemIdentifier = coding.System;

            if (string.IsNullOrWhiteSpace(codeToLookup) || string.IsNullOrWhiteSpace(systemIdentifier))
            {
                throw new ArgumentException("Parameters.coding must include both code and system");
            }
        }
        else
        {
            codeToLookup = code!;
            systemIdentifier = system!;
        }

        var codeGroup = ResolveCodeSystemForLookup(id, systemIdentifier, version);

        if (!codeGroup.Codes.TryGetValue(systemIdentifier, out var codes))
        {
            throw new KeyNotFoundException($"Code system '{systemIdentifier}' was not found in the requested code system");
        }

        var matchedCode = codes.LastOrDefault(c => c.Value == codeToLookup);
        if (matchedCode == null)
        {
            throw new KeyNotFoundException($"Code '{codeToLookup}' was not found in code system '{systemIdentifier}'");
        }

        var response = new Parameters();
        response.Add("name", new FhirString(codeGroup.Name ?? string.Empty));
        response.Add("version", new FhirString(codeGroup.Version ?? string.Empty));
        response.Add("display", new FhirString(matchedCode.Display ?? string.Empty));

        return response;
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

    private static Parameters CreateValidationParameters(bool result, string? message = null, bool isActive = true)
    {
        var parameters = new Parameters();
        parameters.Add("result", new FhirBoolean(result));
        if (message != null)
        {
            parameters.Add("message", new FhirString(message));
        }

        if (!isActive)
        {
            parameters.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "issues",
                Resource = new OperationOutcome
                {
                    Issue =
                    {
                        new OperationOutcome.IssueComponent
                        {
                            Severity = OperationOutcome.IssueSeverity.Warning,
                            Code = OperationOutcome.IssueType.BusinessRule,
                            Details = new CodeableConcept()
                            {
                                Text = "Code is inactive."
                            }
                        }
                    }
                }
            });
        }

        return parameters;
    }

    private Parameters ValidateCodeInCodeGroup(CodeGroup codeGroup, string code, string? system, string? display)
    {
        return string.IsNullOrEmpty(system)
            ? ValidateCodeAcrossSystems(codeGroup, code, display)
            : ValidateCodeInSystem(codeGroup, code, system, display);
    }

    private CodeGroup ResolveCodeSystemForLookup(string? id, string system, string? version)
    {
        if (!string.IsNullOrEmpty(id))
        {
            var byId = cacheService.GetCodeGroupById(CodeGroup.CodeGroupTypes.CodeSystem, id, version);

            if (byId == null)
            {
                if (!string.IsNullOrEmpty(version))
                {
                    throw new KeyNotFoundException($"Code system version '{version}' could not be found");
                }

                throw new KeyNotFoundException($"Code system with id '{id}' was not found");
            }

            return byId;
        }

        var bySystem = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system, version);
        if (bySystem == null || (!string.IsNullOrEmpty(version) && !string.Equals(bySystem.Version, version, StringComparison.CurrentCultureIgnoreCase)))
        {
            if (!string.IsNullOrEmpty(version))
            {
                throw new KeyNotFoundException($"Code system version '{version}' could not be found");
            }

            throw new KeyNotFoundException($"Code system '{system}' was not found");
        }

        return bySystem;
    }

    /// <summary>
    /// Validates a code against a single, known code system within the group.
    /// </summary>
    private Parameters ValidateCodeInSystem(CodeGroup codeGroup, string code, string system, string? display)
    {
        if (!codeGroup.Codes.TryGetValue(system, out var codes))
        {
            return CreateValidationParameters(false, $"Code system not found in {codeGroup.Type}");
        }

        var matches = codes.Where(c => c.Value == code).ToList();

        if (matches.Count == 0)
        {
            return CreateValidationParameters(false, $"Code not found in {codeGroup.Type}");
        }

        return BuildMatchResult(matches, system, display);
    }

    /// <summary>
    /// Validates a code across every code system in the group (no system specified by the caller).
    /// </summary>
    private Parameters ValidateCodeAcrossSystems(CodeGroup codeGroup, string code, string? display)
    {
        foreach (var systemKey in codeGroup.Codes.Keys)
        {
            var matches = codeGroup.Codes[systemKey].Where(c => c.Value == code).ToList();

            if (matches.Count > 0)
            {
                return BuildMatchResult(matches, systemKey, display);
            }
        }

        return CreateValidationParameters(false);
    }

    /// <summary>
    /// Builds the validation result for a set of codes that share the requested value, applying the
    /// display check and surfacing an inactive-code warning when the matched code is inactive.
    /// </summary>
    private Parameters BuildMatchResult(List<Application.Models.Code> matches, string? system, string? display)
    {
        if (!string.IsNullOrEmpty(display) && matches.All(c => c.Display != display))
        {
            return CreateValidationParameters(false, "Display does not match code");
        }

        // Select the last code whose display matches (when supplied); otherwise select the last match.
        var codeObject = !string.IsNullOrEmpty(display)
            ? matches.Last(c => c.Display == display)
            : matches[matches.Count - 1];

        var isActive = ResolveIsActive(codeObject, system);

        return CreateValidationParameters(true, isActive: isActive);
    }

    /// <summary>
    /// Determines whether a matched code is active. CodeSystem members carry their own status; ValueSet members
    /// are plain codes with no status, so their status is rejoined from the CodeSystem identified by <paramref name="system"/>.
    /// Defaults to active when the code cannot be resolved to a loaded CodeSystem, preserving prior behavior.
    /// </summary>
    private bool ResolveIsActive(Application.Models.Code codeObject, string? system)
    {
        if (codeObject is CodeSystemCode codeSystemCode)
        {
            return codeSystemCode.Status == CodeStatus.Active;
        }

        if (string.IsNullOrEmpty(system))
        {
            return true;
        }

        var codeSystemGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system);
        var match = codeSystemGroup?.Codes.Values
            .SelectMany(codes => codes)
            .OfType<CodeSystemCode>()
            .FirstOrDefault(c => c.Value == codeObject.Value);

        return match == null || match.Status == CodeStatus.Active;
    }
}
