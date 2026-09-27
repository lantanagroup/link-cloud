using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Terminology.Application.Models;

/**
 * Represents a group of codes
 */
public class CodeGroup
{
    private readonly Lazy<IReadOnlyList<Code>> _distinctConcepts;

    /// <summary>
    /// Creates an empty code group whose concept index is built on first use.
    /// </summary>
    /// <remarks>
    /// Explicit rather than a field initializer because <see cref="Lazy{T}"/> is handed an instance
    /// method, which a field initializer may not reference.
    /// </remarks>
    public CodeGroup()
    {
        _distinctConcepts = new Lazy<IReadOnlyList<Code>>(
            BuildDistinctConcepts, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public CodeGroupTypes? Type { get; set; }
    public string? Id { get; set; }
    public string? Version { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public List<Identifier> Identifiers { get; set; } = [];
    public Resource? Resource { get; set; }

    // Key is code system URI, value is list of codes
    public Dictionary<string, List<Code>> Codes { get; set; } = new Dictionary<string, List<Code>>();

    /// <summary>
    /// Every concept in the group once, duplicates collapsed last-one-wins, in a stable total order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the read model behind the non-summary <c>GET /CodeSystem?url=</c> response. The
    /// de-duplication used to run per request as <c>GroupBy(c =&gt; c.Value).Select(g =&gt; g.Last())</c>,
    /// which allocated a grouping over every code on every call; for a 375,000-concept code system that
    /// is the per-request cost LEGLINK-968 exists to remove. Computing it once per code group makes a
    /// request's allocation proportional to its page rather than to the code group.
    /// </para>
    /// <para>
    /// The list holds references to the cached <see cref="Code"/> instances, not copies, so the index
    /// costs a pointer per distinct concept on top of codes that are already in memory.
    /// </para>
    /// <para>
    /// <b>Invariant:</b> the index is built from <see cref="Codes"/> on first access and is never
    /// invalidated, so nothing may mutate <see cref="Codes"/> after the index has been read. Both CSV
    /// loaders finish populating <see cref="Codes"/> before the group reaches the cache, and
    /// <c>CodeGroupCacheService.ReplaceCodesFromCsv</c> builds a replacement <see cref="CodeGroup"/>
    /// rather than mutating the cached one -- its own comment already relies on that same immutability
    /// for reader safety, so a replaced group brings a fresh index with it.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Code> DistinctConcepts => _distinctConcepts.Value;

    /// <summary>
    /// Builds <see cref="DistinctConcepts"/> now, so that no request ever pays for it.
    /// </summary>
    /// <remarks>
    /// Called by the CSV loader immediately before the group is published to the cache. Building it
    /// there rather than on first read means the cost lands on the load, and no in-flight request can
    /// be the one that triggers it.
    /// </remarks>
    public void PrewarmConceptIndex() => _ = _distinctConcepts.Value;

    /// <summary>
    /// Collapses duplicate codes last-one-wins while preserving each code's first position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reproduces what <c>GroupBy(c =&gt; c.Value).Select(g =&gt; g.Last())</c> produced: a CSV may list
    /// the same code more than once, and the later row's display is the effective one, but the concept
    /// keeps the position of its first appearance (LEGLINK-599/814). <c>BuildMatchResult</c> and
    /// <c>ResolveCodeStatus</c> resolve the same conflict the same way; the three must agree or a code
    /// reads differently depending on which endpoint answered.
    /// </para>
    /// <para>
    /// System keys are walked in ordinal order rather than dictionary order. A
    /// <see cref="Dictionary{TKey,TValue}"/> makes no guarantee about enumeration order, and paging
    /// needs a total order that is the same on every request or a concept can appear on two pages or on
    /// none. Sorting the keys is O(systems) -- a handful per group -- and never touches a code.
    /// </para>
    /// </remarks>
    private IReadOnlyList<Code> BuildDistinctConcepts()
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var concepts = new List<Code>();

        foreach (var system in Codes.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            foreach (var code in Codes[system])
            {
                // Code.Value is declared required, but that is a compile-time guarantee about
                // initialization and says nothing about what CsvHelper assigns: both loaders run with
                // MissingFieldFound = null, so a short row leaves it null. Keying on "" rather than
                // faulting matches CodeSearchService, which tolerates the same rows.
                var value = code.Value ?? string.Empty;

                if (positions.TryGetValue(value, out var at))
                {
                    concepts[at] = code;
                }
                else
                {
                    positions[value] = concepts.Count;
                    concepts.Add(code);
                }
            }
        }

        return concepts;
    }

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
