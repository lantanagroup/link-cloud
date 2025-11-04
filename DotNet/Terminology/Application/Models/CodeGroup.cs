using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Terminology.Application.Models;

/**
 * Represents a group of codes
 */
public class CodeGroup
{
    public CodeGroupTypes? Type { get; set; }
    public string? Id { get; set; }
    public string? Version { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public List<Identifier> Identifiers { get; set; } = [];
    public Resource? Resource { get; set; }
    
    // Key is code system URI, value is list of codes
    public Dictionary<string, List<Code>> Codes { get; set; } = new Dictionary<string, List<Code>>();

    public enum CodeGroupTypes
    {
        CodeSystem,
        ValueSet
    }

    public override string ToString()
    {
        return $"{Type}|{Url}|{Version}".ToLowerInvariant();
    }
}