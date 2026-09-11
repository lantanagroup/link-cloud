namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

/// <summary>
/// Caps how many <em>filter</em> IDs (encounter, _id, …) go on one FHIR search.
/// This is not result paging: 68 encounter IDs means "ServiceRequests for these
/// 68 encounters," and each encounter may return many resources. Result pages
/// are controlled separately with <c>_count</c>.
/// </summary>
public static class FhirSearchLimits
{
    public const int MaxIdsPerParameter = 100;

    /// <summary>
    /// If any query parameter value has more than <paramref name="maxIds"/>
    /// comma-separated tokens, split every oversized parameter into chunks and
    /// return the cartesian product of those chunks (FHIR ANDs distinct params).
    /// Other parameters are copied onto every batch.
    /// </summary>
    public static List<List<string>> SplitOversizedIdParameters(
        IReadOnlyList<string>? queryParameters,
        int maxIds = MaxIdsPerParameter)
    {
        if (maxIds < 1)
            maxIds = MaxIdsPerParameter;

        var parameters = queryParameters?.ToList() ?? [];
        if (parameters.Count == 0)
            return [[]];

        var oversized = new List<(int Index, string Key, string[] Tokens)>();
        for (var i = 0; i < parameters.Count; i++)
        {
            var parts = SplitKeyValue(parameters[i]);
            if (parts == null)
                continue;

            var tokens = parts.Value.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length > maxIds)
                oversized.Add((i, parts.Value.Key, tokens));
        }

        if (oversized.Count == 0)
            return [parameters];

        var batches = new List<List<string>> { parameters };
        foreach (var (index, key, tokens) in oversized)
        {
            var next = new List<List<string>>();
            foreach (var batch in batches)
            {
                foreach (var chunk in tokens.Chunk(maxIds))
                {
                    var copy = new List<string>(batch);
                    copy[index] = $"{key}={string.Join(',', chunk)}";
                    next.Add(copy);
                }
            }

            batches = next;
        }

        return batches;
    }

    private static (string Key, string Value)? SplitKeyValue(string parameter)
    {
        var separator = parameter.IndexOf('=');
        if (separator <= 0 || separator == parameter.Length - 1)
            return null;

        return (parameter[..separator], parameter[(separator + 1)..]);
    }
}
