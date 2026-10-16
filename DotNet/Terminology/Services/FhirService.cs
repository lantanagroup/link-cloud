using Amazon.Runtime.Internal;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Terminology.Application.Extensions;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Application.Settings;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;

namespace LantanaGroup.Link.Terminology.Services;

/**
 * Service for FHIR terminology operations. Implements portions of FHIR terminology, as defined in these specifications:
 * https://build.fhir.org/valueset-operation-expand.html
 * https://build.fhir.org/codesystem-operation-validate-code.html
 * https://build.fhir.org/valueset-operation-validate-code.html
 */
public class FhirService(
    ICodeGroupCacheService cacheService,
    ILogger<FhirService> logger,
    ITerminologyServiceMetrics metrics,
    IOptions<TerminologyConfig> terminologyConfig)
{
    private readonly TerminologyConfig _config = terminologyConfig.Value;

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

    /// <summary>
    /// Searches for value sets, optionally expanding a single named one.
    /// </summary>
    /// <remarks>
    /// The non-summary form embeds one page of the value set's codes in
    /// <c>ValueSet.expansion</c>, grouped under one entry per code system.
    /// <c>expansion.total</c> counts the <b>codes</b>, not those grouping entries, so a client must not
    /// infer its progress from the length of <c>contains</c>. Paging follows <c>count</c>/<c>offset</c>
    /// exactly as <c>$expand</c> does; both are ignored when <paramref name="summary"/> is
    /// <see cref="SummaryType.True"/>, which returns the stored resource unexpanded as before.
    /// </remarks>
    /// <param name="url">The canonical URL of a single value set, or null to list them all.</param>
    /// <param name="summary">Whether to return the summary form.</param>
    /// <param name="count">Codes per page; null applies the configured default.</param>
    /// <param name="offset">Zero-based index of the first code; null starts at the beginning.</param>
    /// <exception cref="ArgumentException">
    /// No url was supplied and no summary requested, or count or offset was negative.
    /// </exception>
    public Bundle GetValueSets(string? url, SummaryType? summary, int? count = null, int? offset = null)
    {
        if (string.IsNullOrEmpty(url) && summary == null)
        {
            logger.LogError("No url or summary parameter specified while searching for all value sets (no url specified)");
            throw new ArgumentException("Must specify url if summary is not requested");
        }

        // Resolved before the lookup so that a malformed count or offset is reported as the bad request
        // it is whether or not the named value set turns out to be loaded. Summary mode ignores both,
        // so it is not validated there either -- that path is unchanged.
        var page = summary == SummaryType.True ? default : ResolvePage(count, offset);

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

                // If not summary mode, then enumerate one page of the value set's codes into the
                // expansion.contains property. Before LEGLINK-968 this enumerated every code, which for
                // a 421,970-code value set allocated roughly 85 MB of element POCOs per request.
                if (summary != SummaryType.True)
                {
                    clone.Expansion = NewExpansion(page, CountCodes(codeGroup));

                    // The historical shape is one grouping entry per code system carrying its codes as
                    // children, rather than the flat list $expand returns. It is preserved because an
                    // out-of-repo client may be reading it, so a new grouper is opened whenever the
                    // system changes within the page -- a page that straddles a system boundary emits
                    // two, each holding only the codes that fell inside the page.
                    ValueSet.ContainsComponent? grouper = null;
                    string? grouperSystem = null;

                    foreach (var (system, code) in EnumerateCodes(codeGroup, page.Offset).Take(page.Count))
                    {
                        if (grouper is null || !string.Equals(grouperSystem, system, StringComparison.Ordinal))
                        {
                            grouperSystem = system;
                            grouper = new ValueSet.ContainsComponent { System = system };
                            clone.Expansion.Contains.Add(grouper);
                        }

                        grouper.Contains.Add(new ValueSet.ContainsComponent()
                        {
                            Code = code.Value,
                            Display = code.Display
                        });
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

    /// <summary>
    /// Expands a value set into one page of its codes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The expansion is bounded. A request naming no <c>count</c> gets the configured default page
    /// rather than every code, and a <c>count</c> above the configured maximum is reduced to it rather
    /// than refused. <c>expansion.total</c> always reports the full size, and <c>expansion.offset</c>
    /// and <c>expansion.parameter</c> report the page actually returned, so a client can page through
    /// the whole value set. <c>count=0</c> returns the total with no codes.
    /// </para>
    /// <para>
    /// <paramref name="date"/> is accepted and ignored. The cache holds only the currently loaded
    /// version of each value set, so there is no historical content for it to select.
    /// </para>
    /// </remarks>
    /// <param name="id">The resource id of the value set. Optional if a url is given.</param>
    /// <param name="url">The canonical url of the value set. Optional if an id is given.</param>
    /// <param name="date">Accepted for FHIR conformance and ignored.</param>
    /// <param name="count">Codes per page; null applies the configured default.</param>
    /// <param name="offset">Zero-based index of the first code; null starts at the beginning.</param>
    /// <exception cref="ArgumentException">
    /// Neither id nor url was supplied, or count or offset was negative.
    /// </exception>
    /// <exception cref="KeyNotFoundException">No such value set is loaded.</exception>
    public ValueSet ExpandValueSet(string? id, string? url, string? date, int? count = null, int? offset = null)
    {
        // Resolved before the lookup, so a request that is both malformed and names a value set that is
        // not loaded is reported as the 400 it is rather than as a 404. Keep this first.
        var page = ResolvePage(count, offset);

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

        valueSetCopy.Expansion = NewExpansion(page, CountCodes(codeGroup));

        foreach (var (system, code) in EnumerateCodes(codeGroup, page.Offset).Take(page.Count))
        {
            valueSetCopy.Expansion.Contains.Add(new ValueSet.ContainsComponent
            {
                System = system,
                Code = code.Value,
                Display = code.Display
            });
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

        var codeSystem = codeGroup.Resource as CodeSystem;
        if(codeSystem != null && codeSystem.Content == CodeSystemContentMode.NotPresent && codeGroup.Codes.Values.Any(c => c.Count > 0))
            codeSystem.Content = CodeSystemContentMode.Complete;

        return codeSystem;
    }

    /// <summary>
    /// Searches for code systems, optionally including a page of a single named one's concepts.
    /// </summary>
    /// <remarks>
    /// The non-summary form carries one page of concepts. <c>CodeSystem.count</c> always reports the
    /// full number of distinct concepts, and <c>CodeSystem.content</c> reports <c>fragment</c> whenever
    /// the page is a subset -- a partial list described as <c>complete</c> would tell a client it had
    /// the whole code system. <paramref name="count"/> and <paramref name="offset"/> are ignored when
    /// <paramref name="summary"/> is <see cref="SummaryType.True"/>, which is unchanged.
    /// </remarks>
    /// <param name="url">The canonical URL of a single code system, or null to list them all.</param>
    /// <param name="summary">Whether to return the summary form.</param>
    /// <param name="count">Concepts per page; null applies the configured default.</param>
    /// <param name="offset">Zero-based index of the first concept; null starts at the beginning.</param>
    /// <exception cref="ArgumentException">
    /// No url was supplied and no summary requested, or count or offset was negative.
    /// </exception>
    public Bundle GetCodeSystems(string? url, SummaryType? summary, int? count = null, int? offset = null)
    {
        if (string.IsNullOrEmpty(url) && (summary == null))
        {
            logger.LogError("No url or summary parameter specified while searching for all code systems (no url specified)");
            throw new ArgumentException("Must specify url if summary is not requested");
        }

        // Resolved before the lookup so that a malformed count or offset is reported as the bad request
        // it is whether or not the named code system turns out to be loaded. Summary mode ignores both,
        // so it is not validated there either -- that path is unchanged.
        var page = summary == SummaryType.True ? default : ResolvePage(count, offset);

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

                if (summary == SummaryType.True)
                {
                    // Unchanged by LEGLINK-968. Reporting "complete" on a response that carries no
                    // concepts is wrong per spec, but the summary form is what every real caller uses
                    // and HAPI's validation support reads content to decide whether to expand a code
                    // system in process, so correcting it needs its own before/after comparison.
                    if (clone.Content == CodeSystemContentMode.NotPresent && codeGroup.Codes.Values.Any(c => c.Count > 0))
                        clone.Content = CodeSystemContentMode.Complete;
                }
                else
                {
                    logger.LogDebug("Search performed without summary mode for code system {Url}", url.SanitizeAndRemove());

                    // A CSV may list the same code more than once; each code is emitted once, keeping
                    // the last occurrence's display so the read agrees with last-one-wins (LEGLINK-814).
                    // The de-duplication is memoized on the code group at load time rather than redone
                    // here, so the cost of a request is its page and not the whole code system.
                    var concepts = codeGroup.DistinctConcepts;

                    // The full count, even when only a page is carried, so a client knows what it is
                    // paging through.
                    clone.Count = concepts.Count;

                    // Computed in 64-bit: both operands are int, and an offset near int.MaxValue
                    // overflows the sum to a negative bound that would read from a negative index.
                    var end = (int)Math.Min((long)page.Offset + page.Count, concepts.Count);

                    for (var index = page.Offset; index < end; index++)
                    {
                        clone.Concept.Add(new CodeSystem.ConceptDefinitionComponent()
                        {
                            Code = concepts[index].Value,
                            Display = concepts[index].Display
                        });
                    }

                    // A page carrying every concept is complete, a page carrying some of them is a
                    // fragment, and a page carrying none of them is not-present. A code group with no
                    // concepts at all leaves whatever the stored resource declared alone: there is
                    // nothing to describe, and the pre-LEGLINK-968 code likewise only ever upgraded
                    // not-present when there was at least one code to justify it.
                    if (concepts.Count > 0)
                    {
                        clone.Content = clone.Concept.Count == concepts.Count
                            ? CodeSystemContentMode.Complete
                            : clone.Concept.Count == 0
                                ? CodeSystemContentMode.NotPresent
                                : CodeSystemContentMode.Fragment;
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
        var started = Stopwatch.GetTimestamp();
        var cache = "miss";
        var outcome = "success";
        try
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
            outcome = "not_found";
            return CreateValidationParameters(false, "Code system not found");
        }

        cache = "hit";

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
        catch
        {
            outcome = "failure";
            throw;
        }
        finally
        {
            RecordLookup("codesystem", outcome, cache, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    public Parameters ValidateCodeInValueSet(string? url, string? id, string? system, string? code, string? display, Parameters? parameters)
    {
        var started = Stopwatch.GetTimestamp();
        var cache = "miss";
        var outcome = "success";
        try
        {
        var urlComponent = parameters?.Get("url").FirstOrDefault();
        var systemComponent = parameters?.Get("system").FirstOrDefault();
        var codeComponent = parameters?.Get("code").FirstOrDefault();
        var displayComponent = parameters?.Get("display").FirstOrDefault();
        var coding = parameters?.Get("coding").FirstOrDefault()?.Value as Coding;
        var codeableConcept = parameters?.Get("codeableConcept").FirstOrDefault()?.Value as CodeableConcept;

        // Every client-supplied system is normalized here, ahead of the merges below and of the value set
        // lookup. The merges treat an empty string as "not supplied", so a blank reaching them would be
        // silently overwritten and never rejected; validating up front also keeps the answer for a
        // malformed codeableConcept independent of which coding happens to match first (LEGLINK-888).
        system = NormalizeSystem(system, "system");
        var bodySystem = NormalizeSystem(systemComponent?.Value?.ToString(), "system");
        var codingSystem = coding == null ? null : NormalizeSystem(coding.System, "coding.system");
        var conceptSystems = (codeableConcept?.Coding ?? [])
            .Select(conceptCoding => NormalizeSystem(conceptCoding.System, "codeableConcept.coding.system"))
            .ToList();

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
        else if (!string.IsNullOrEmpty(url))
        {
            codeGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, url);
        }
        else
        {
            // A request that identifies no value set is malformed, not a failed validation. Throwing
            // surfaces it as a 400 and matches ValidateCodeInCodeSystem; returning result=false here
            // reported a well-formed "this code is invalid" answer to a question never asked (LEGLINK-887).
            throw new ArgumentException("No id or url parameter specified");
        }

        if (codeGroup == null)
        {
            outcome = "not_found";
            return CreateValidationParameters(false, "Value set not found");
        }

        cache = "hit";

        // Priority 1: Direct parameters
        if (!string.IsNullOrEmpty(code))
        {
            // A normalized system is either null or a real value, so "not supplied" is a null check.
            // string.IsNullOrEmpty here would fold a blank back into the absent case (LEGLINK-888).
            if (bodySystem != null && system == null)
            {
                system = bodySystem;
            }

            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
            {
                display = displayComponent.Value.ToString();
            }

            return ValidateCodeInCodeGroup(codeGroup, code, system, display);
        }

        // Priority 2: Parameters.code and Parameters.system
        if (codeComponent?.Value != null)
        {
            if (bodySystem != null && system == null)
            {
                system = bodySystem;
            }

            if (displayComponent?.Value != null && string.IsNullOrEmpty(display))
            {
                display = displayComponent.Value.ToString();
            }

            return ValidateCodeInCodeGroup(codeGroup, codeComponent.Value.ToString(), system, display);
        }

        // Priority 3: Parameters.coding
        if (coding != null)
        {
            return ValidateCodeInCodeGroup(codeGroup, coding.Code, codingSystem, coding.Display);
        }

        // Priority 4: Parameters.codeableConcept
        if (codeableConcept?.Coding != null)
        {
            foreach (var (conceptCoding, conceptSystem) in codeableConcept.Coding.Zip(conceptSystems))
            {
                var result = ValidateCodeInCodeGroup(codeGroup, conceptCoding.Code, conceptSystem, conceptCoding.Display);
                var resultBoolean = result.GetSingleValue<FhirBoolean>("result");
                if (resultBoolean?.Value == true)
                {
                    return result;
                }
            }
        }

        return CreateValidationParameters(false, "No valid code found in parameters");
        }
        catch
        {
            outcome = "failure";
            throw;
        }
        finally
        {
            RecordLookup("valueset", outcome, cache, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
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
                },
                new()
                {
                    Name = "lookup",
                    Definition = "http://hl7.org/fhir/OperationDefinition/CodeSystem-lookup",
                    Documentation = "Lookup a code in a code system"
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

    /// <summary>
    /// Resolves a caller's paging parameters against this server's configured bounds.
    /// </summary>
    private ExpansionPage ResolvePage(int? count, int? offset) => ExpansionPaging.Resolve(
        count, offset, _config.DefaultExpansionPageSize, _config.MaxExpansionPageSize);

    /// <summary>
    /// The number of codes in the group, counting duplicates, without enumerating any of them.
    /// </summary>
    /// <remarks>
    /// Summed in 64-bit and saturated, because <c>ValueSet.expansion.total</c> is an <c>int</c> and a
    /// group large enough to overflow one would report a negative total rather than a large one. Not
    /// reachable with real terminology content; the cast costs nothing.
    /// </remarks>
    private static int CountCodes(CodeGroup codeGroup)
    {
        long total = 0;

        foreach (var codes in codeGroup.Codes.Values)
        {
            total += codes.Count;
        }

        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    /// <summary>
    /// Enumerates a code group's codes in a stable total order, skipping the first
    /// <paramref name="skip"/> of them without materializing anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is: code system URI ascending ordinal, then the order the codes were loaded in. It has
    /// to be total and repeatable or paging is broken -- a pair of codes that compare equal can swap
    /// between two requests, and a code then appears on two pages or on none.
    /// <see cref="Dictionary{TKey,TValue}"/> guarantees nothing about enumeration order, so the keys are
    /// sorted; there are a handful of them per group, so this never touches a code.
    /// </para>
    /// <para>
    /// Whole systems are skipped by their <c>Count</c>, so a large offset costs a few integer
    /// comparisons rather than walking the codes it is skipping over. That, plus the caller's
    /// <c>Take</c>, is what bounds a request's allocation by its page size rather than by the size of
    /// the code group (LEGLINK-968).
    /// </para>
    /// </remarks>
    private static IEnumerable<(string System, Code Code)> EnumerateCodes(CodeGroup codeGroup, int skip)
    {
        foreach (var system in codeGroup.Codes.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            var codes = codeGroup.Codes[system];

            if (skip >= codes.Count)
            {
                skip -= codes.Count;
                continue;
            }

            for (var index = skip; index < codes.Count; index++)
            {
                yield return (system, codes[index]);
            }

            skip = 0;
        }
    }

    /// <summary>
    /// Builds the expansion envelope carried by every paged response.
    /// </summary>
    /// <remarks>
    /// <c>total</c> and <c>offset</c> are what let a client page correctly, and the spec requires both
    /// on a partial expansion. <c>timestamp</c> is 1..1 in R4 and was never populated before
    /// LEGLINK-968. The echoed <c>count</c> and <c>offset</c> parameters report the values actually
    /// applied, not the ones requested, so a caller whose oversized <c>count</c> was reduced to the
    /// server maximum can see that from the response instead of inferring it from a short page.
    /// </remarks>
    private static ValueSet.ExpansionComponent NewExpansion(ExpansionPage page, int total)
    {
        return new ValueSet.ExpansionComponent
        {
            Identifier = $"urn:uuid:{Guid.NewGuid()}",
            TimestampElement = FhirDateTime.Now(),
            Total = total,
            Offset = page.Offset,
            Parameter =
            {
                new ValueSet.ParameterComponent
                {
                    Name = ExpansionParameterNames.Count,
                    Value = new Integer(page.Count)
                },
                new ValueSet.ParameterComponent
                {
                    Name = ExpansionParameterNames.Offset,
                    Value = new Integer(page.Offset)
                }
            }
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

    /// <summary>
    /// Placeholders a client produces by interpolating an unset variable into a request rather than
    /// omitting the parameter — JavaScript renders null and undefined this way. They are accepted as
    /// meaning "no system supplied".
    /// </summary>
    private void RecordLookup(string groupKind, string outcome, string cache, double durationMilliseconds)
    {
        metrics.IncrementLookupCount(outcome, groupKind);
        if (MetricsModeScope.IsPerformance)
        {
            metrics.RecordLookupDuration(durationMilliseconds, groupKind, cache);
        }
    }

    private static readonly string[] SystemPlaceholders = ["null", "undefined"];

    /// <summary>
    /// Validates and normalizes a client-supplied <c>system</c> for a ValueSet $validate-code request,
    /// mapping it onto the two states the validator understands: a real system to look up, or null
    /// meaning "search every system in the value set".
    /// </summary>
    /// <remarks>
    /// An absent system is legitimate FHIR and keeps its search-all-systems meaning. A blank one is not:
    /// no FHIR primitive may be an empty string, so the request is malformed and is rejected rather than
    /// reinterpreted. Folding a blank into the absent case answered a broader question than the caller
    /// asked and reported result=true without disclosing which system matched (LEGLINK-888).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is present but blank, which the caller surfaces as a 400.
    /// </exception>
    private static string? NormalizeSystem(string? system, string parameterName)
    {
        if (system == null)
        {
            return null;
        }

        // FHIR requires at least one character of non-whitespace content, so "   " is as malformed as "".
        // Checked before trimming, so a whitespace-only value is rejected rather than trimmed to empty.
        if (string.IsNullOrWhiteSpace(system))
        {
            throw new ArgumentException($"The '{parameterName}' parameter cannot be blank");
        }

        // Surrounding whitespace is not significant, but the lookup in ValidateCodeInSystem is an exact
        // dictionary match, so an untrimmed " http://x " reports "Code system not found" for a system
        // that is present -- the same confidently-wrong answer to a malformed request this method exists
        // to prevent. Trimming also lets a padded placeholder resolve to null.
        var trimmed = system.Trim();

        return SystemPlaceholders.Contains(trimmed, StringComparer.OrdinalIgnoreCase) ? null : trimmed;
    }

    private Parameters ValidateCodeInCodeGroup(CodeGroup codeGroup, string code, string? system, string? display)
    {
        // string.IsNullOrEmpty rather than a null check: this is shared with ValidateCodeInCodeSystem,
        // which passes the code group's own Url here. That is cache content, not client input, so an
        // empty one must keep falling back to a search rather than being blamed on the caller.
        // ValueSet input reaching this point has already been through NormalizeSystem.
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

        var bySystem = cacheService.GetCodeGroupExact(CodeGroup.CodeGroupTypes.CodeSystem, system, version);
        if (bySystem == null)
        {
            throw new KeyNotFoundException(!string.IsNullOrEmpty(version)
                ? $"Code system version '{version}' could not be found"
                : $"Code system '{system}' was not found");
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

        // Carry the same message as the single-system path. Omitting it left callers with a bare
        // result=false — the Java validation support reads the "message" parameter to populate its
        // CodeValidationResult, so the failure reached the report with no explanation (LEGLINK-886).
        return CreateValidationParameters(false, $"Code not found in {codeGroup.Type}");
    }

    /// <summary>
    /// Builds the validation result for a set of codes that share the requested value, applying the
    /// display check and surfacing an inactive-code warning when the matched code is inactive.
    /// </summary>
    private Parameters BuildMatchResult(List<Code> matches, string? system, string? display)
    {
        if (!string.IsNullOrEmpty(display) && matches.All(c => c.Display != display))
        {
            return CreateValidationParameters(false, "Display does not match code");
        }

        // Select the last code whose display matches (when supplied); otherwise select the last match.
        var codeObject = !string.IsNullOrEmpty(display)
            ? matches.Last(c => c.Display == display)
            : matches[matches.Count - 1];

        var isActive = ResolveCodeStatus(codeObject, system) == CodeStatus.Active;

        return CreateValidationParameters(true, isActive: isActive);
    }

    /// <summary>
    /// Resolves the effective status of a matched code. CodeSystem members carry their own status. ValueSet members
    /// that carry a membership status (loaded as a <see cref="ValueSetCode"/>) are authoritative and override the
    /// code system. ValueSet members with no membership status (plain <see cref="Application.Models.Code"/>) have
    /// their status rejoined from the CodeSystem identified by <paramref name="system"/>.
    /// Defaults to active when the code cannot be resolved to a loaded CodeSystem, preserving prior behavior.
    /// </summary>
    /// <remarks>
    /// Public so <c>ConfigController</c>'s cached-code lookups report the same status the corresponding
    /// <c>$validate-code</c> call would. The two disagreeing about one code is what LEGLINK-889 was raised over,
    /// so there is deliberately one implementation rather than two.
    /// </remarks>
    public CodeStatus ResolveCodeStatus(Code codeObject, string? system)
    {
        if (codeObject is CodeSystemCode codeSystemCode)
        {
            return codeSystemCode.Status;
        }

        // Value set membership status, when present, is authoritative and overrides the code system.
        if (codeObject is ValueSetCode valueSetCode)
        {
            return valueSetCode.Status;
        }

        if (string.IsNullOrEmpty(system))
        {
            return CodeStatus.Active;
        }

        var codeSystemGroup = cacheService.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, system);
        // When the CodeSystem lists the code more than once with differing status, the last
        // occurrence wins (LEGLINK-599/814), matching BuildMatchResult's last-match selection.
        var match = codeSystemGroup?.Codes.Values
            .SelectMany(codes => codes)
            .OfType<CodeSystemCode>()
            .LastOrDefault(c => c.Value == codeObject.Value);

        return match?.Status ?? CodeStatus.Active;
    }
}
