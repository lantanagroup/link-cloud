namespace Terminology.Application.Models;

public class CacheKey: IComparable<CacheKey>
{
    public CodeGroup.CodeGroupTypes Type { get; set; }
    public string Url { get; set; }
    public string Version { get; set; }
    public string Id { get; set; }
    public string Key => $"{Type}|{Url}|{Version}".ToLower();

    public CacheKey(CodeGroup.CodeGroupTypes type, string url, string version, string id)
    {
        Type = type;
        Url = url;
        Version = version;
        Id = id;
    }

    public CacheKey(CodeGroup codeGroup)
    {
        Type = codeGroup.Type;
        Url = codeGroup.Url;
        Version = codeGroup.Version;
        Id = codeGroup.Id;
    }

    public int CompareTo(CacheKey? other)
    {
        if (other == null) return 1;
        return string.Compare(Key, other.Key, StringComparison.Ordinal);
    }
}