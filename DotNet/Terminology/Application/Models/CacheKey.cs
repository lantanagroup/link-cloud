using Hl7.Fhir.Model;

namespace Terminology.Application.Models;

public class CacheKey: IComparable<CacheKey>
{
    public CodeGroup.CodeGroupTypes Type { get; set; }
    public string Url { get; set; }
    public string Version { get; set; }
    public string Id { get; set; }
    public string Key => $"{Type}|{Url}|{Version}".ToLower();
    public List<Identifier> Identifiers { get; set; } = new List<Identifier>();

    public CacheKey(CodeGroup.CodeGroupTypes type, string url, string version, string id, List<Identifier> identifiers)
    {
        Type = type;
        Url = url;
        Version = version;
        Id = id;
        Identifiers = identifiers;
    }

    public int CompareTo(CacheKey? other)
    {
        if (other == null) return 1;
        return string.Compare(Key, other.Key, StringComparison.Ordinal);
    }
}