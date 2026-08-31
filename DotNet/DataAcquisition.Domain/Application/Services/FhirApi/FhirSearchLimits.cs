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
    /// If one query parameter value has more than <paramref name="maxIds"/>
    /// comma-separated tokens, return one copy of the parameter list per chunk.
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

        var oversizedIndex = -1;
        string[]? oversizedTokens = null;
        for (var i = 0; i < parameters.Count; i++)
        {
            var parts = SplitKeyValue(parameters[i]);
            if (parts == null)
                continue;

            var tokens = parts.Value.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length > maxIds)
            {
                oversizedIndex = i;
                oversizedTokens = tokens;
                break;
            }
        }

        if (oversizedIndex < 0 || oversizedTokens == null)
            return [parameters];

        var key = SplitKeyValue(parameters[oversizedIndex])!.Value.Key;
        var batches = new List<List<string>>();
        foreach (var chunk in oversizedTokens.Chunk(maxIds))
        {
            var batch = new List<string>(parameters.Count);
            for (var i = 0; i < parameters.Count; i++)
            {
                batch.Add(i == oversizedIndex ? $"{key}={string.Join(',', chunk)}" : parameters[i]);
            }

            batches.Add(batch);
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
