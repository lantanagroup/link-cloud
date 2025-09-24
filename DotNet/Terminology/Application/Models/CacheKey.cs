using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// Represents a unique identifier for caching code groups, consisting of type, URL, version, and ID.
/// </summary>
public class CacheKey : IComparable<CacheKey>
{
    private CodeGroup.CodeGroupTypes _type;
    private string _url;
    private string _version;
    private string _cachedKey;

    /// <summary>
    /// Gets or sets the type of the code group, which determines the classification or kind of the code group.
    /// </summary>
    /// <remarks>
    /// The type is represented by the <see cref="CodeGroup.CodeGroupTypes"/> enumeration, which includes values
    /// such as CodeSystem or ValueSet. This property is used as a key component in identifying and categorizing
    /// code groups within the caching mechanism.
    /// </remarks>
    public CodeGroup.CodeGroupTypes Type
    {
        get => _type;
        set
        {
            _type = value;
            UpdateCachedKey();
        }
    }

    /// <summary>
    /// Gets or sets the URL associated with the cache key, representing the canonical url of the resource for identifying or retrieving cached data.
    /// </summary>
    /// <remarks>
    /// The URL is used as part of the cache key composition to uniquely identify a set of code groups,
    /// thereby playing a key role in differentiating resources in the caching mechanism.
    /// </remarks>
    public string Url
    {
        get => _url;
        set
        {
            _url = value;
            UpdateCachedKey();
        }
    }

    /// <summary>
    /// Gets or sets the version of the code group, representing the specific iteration or release of the group (i.e. ValueSet.version or CodeSystem.version).
    /// </summary>
    /// <remarks>
    /// The version is used in conjunction with the type, URL, and ID to uniquely identify and manage code groups
    /// within the caching mechanism. A null or empty value may imply the latest version or an unspecified version,
    /// depending on the context.
    /// </remarks>
    public string Version
    {
        get => _version;
        set
        {
            _version = value;
            UpdateCachedKey();
        }
    }

    /// <summary>
    /// Gets or sets the unique identifier for this cache key, which distinguishes
    /// the specific code group within the cache mechanism.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets a unique, lowercase key that combines the type, URL, and version of the code group.
    /// </summary>
    /// <remarks>
    /// The key is formatted as a concatenation of the type, URL, and version properties, separated by a pipe ('|').
    /// It is used as a unique identifier for caching and retrieval operations.
    /// </remarks>
    public string Key => _cachedKey;

    /// <summary>
    /// Gets or sets the collection of <see cref="Identifier"/> instances associated with the cache key.
    /// </summary>
    /// <remarks>
    /// This property is used to store additional identifiers linked to the cache key, which may include alternative
    /// or supplementary means of identifying the code group. Each <see cref="Identifier"/> includes relevant
    /// identification details, which can aid in resolving and retrieving code groups from the cache.
    /// </remarks>
    public List<Identifier> Identifiers { get; set; } = new();

    /// <summary>
    /// Updates the cached key value when Type, URL, or Version changes.
    /// </summary>
    private void UpdateCachedKey()
    {
        _cachedKey = $"{_type}|{_url}|{_version}".ToLowerInvariant();
    }

    /// <summary>
    /// Represents a unique identifier used in caching operations for code groups. A CacheKey is composed of a type, URL, and version to distinguish code groups in a caching mechanism.
    /// </summary>
    public CacheKey(CodeGroup.CodeGroupTypes type, string url, string version, string id, List<Identifier> identifiers)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(id);
        
        _type = type;
        _url = url;
        _version = version;
        Id = id;
        Identifiers = identifiers;
        UpdateCachedKey();
    }

    /// <summary>
    /// Compares the current CacheKey instance with another CacheKey and determines their relative order based on the composite key (type, URL, and version).
    /// </summary>
    /// <param name="other">The CacheKey instance to compare with the current instance.</param>
    /// <returns>An integer indicating the relative order:
    /// -1 if the current instance precedes the other,
    /// 0 if both are equal,
    /// or 1 if the current instance follows the other.</returns>
    public int CompareTo(CacheKey? other)
    {
        if (other == null) return 1;
        return string.Compare(Key, other.Key, StringComparison.Ordinal);
    }
}