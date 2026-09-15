using Automation.UI.Models.ApiHealth;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Provides MongoDB persistence for API health test run results.
/// </summary>
public interface IApiHealthRunStore
{
    Task SaveRunResultsAsync(IEnumerable<ApiTestRunResult> results, string runMode, DateTimeOffset startedAt, CancellationToken ct = default);
    Task<Dictionary<string, ApiTestRunResult>> GetLatestResultsByServiceAsync(IEnumerable<string> endpointKeys, CancellationToken ct = default);
    Task<Dictionary<string, ApiTestRunResult>> GetLatestResultsForRunAsync(Guid runId, IEnumerable<string> endpointKeys, CancellationToken ct = default);
    Task SaveServiceRunStateAsync(Guid runId, string runMode, IEnumerable<string> serviceNames, DateTimeOffset startedAt, CancellationToken ct = default);
    Task<ApiHealthLatestRunContext?> GetLatestRunContextAsync(CancellationToken ct = default);
    Task<ApiTestRunHistoryPage> GetHistoryAsync(string endpointKey, int pageNumber, int pageSize, CancellationToken ct = default);
    Task UpsertExecutionRunStatusAsync(ApiHealthExecutionRunStatus status, CancellationToken ct = default);
    Task AttachSeedRunAsync(Guid runId, Guid seedRunId, string? seedRunName, CancellationToken ct = default);
    Task<ApiHealthExecutionRunStatus?> GetActiveExecutionRunStatusAsync(CancellationToken ct = default);
    Task<ApiHealthExecutionRunStatus?> GetLatestExecutionRunStatusAsync(CancellationToken ct = default);
    Task<ApiHealthExecutionRunStatus?> GetExecutionRunStatusAsync(Guid runId, CancellationToken ct = default);
    Task CompleteExecutionRunAsync(Guid runId, bool failed, string? error, DateTimeOffset finishedAt, CancellationToken ct = default);
}

public sealed class MongoApiHealthRunStore : IApiHealthRunStore
{
    private readonly IMongoCollection<ApiHealthRunDocument> _collection;
    private readonly IMongoCollection<ApiHealthExecutionRunDocument> _executionCollection;
    private readonly IMongoCollection<ApiHealthRunResultDocument> _resultCollection;

    public MongoApiHealthRunStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<ApiHealthRunDocument>("api_health_runs");
        _executionCollection = database.GetCollection<ApiHealthExecutionRunDocument>("api_health_execution_runs");
        _resultCollection = database.GetCollection<ApiHealthRunResultDocument>("api_health_run_results");
    }

    public async Task SaveRunResultsAsync(
        IEnumerable<ApiTestRunResult> results,
        string runMode,
        DateTimeOffset startedAt,
        CancellationToken ct = default)
    {
        var normalizedMode = string.Equals(runMode, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "Single";
        var resultGroups = results
            .Where(r => !string.IsNullOrWhiteSpace(r.ServiceName))
            .GroupBy(r => (r.RunId, ServiceName: r.ServiceName), RunServiceGroupKeyComparer.Instance)
            .ToList();

        foreach (var group in resultGroups)
        {
            var runId = group.Key.RunId;
            if (runId == Guid.Empty)
                continue;

            var filter = Builders<ApiHealthRunDocument>.Filter.And(
                Builders<ApiHealthRunDocument>.Filter.Eq(d => d.RunId, runId),
                Builders<ApiHealthRunDocument>.Filter.Eq(d => d.ServiceName, group.Key.ServiceName));

            var runUpdate = Builders<ApiHealthRunDocument>.Update
                .SetOnInsert(d => d.Id, Guid.NewGuid())
                .SetOnInsert(d => d.RunId, runId)
                .SetOnInsert(d => d.ServiceName, group.Key.ServiceName)
                .Set(d => d.RunMode, normalizedMode)
                .Set(d => d.StartedAt, startedAt)
                .SetOnInsert(d => d.EndpointResults, []);

            await _collection.UpdateOneAsync(
                filter,
                runUpdate,
                new UpdateOptions { IsUpsert = true },
                ct);

            foreach (var result in group)
            {
                var resultFilter = Builders<ApiHealthRunResultDocument>.Filter.And(
                    Builders<ApiHealthRunResultDocument>.Filter.Eq(d => d.RunId, runId),
                    Builders<ApiHealthRunResultDocument>.Filter.Eq(
                        d => d.ServiceName,
                        group.Key.ServiceName),
                    Builders<ApiHealthRunResultDocument>.Filter.Eq(
                        d => d.EndpointKey,
                        result.EndpointKey));

                var resultUpdate = Builders<ApiHealthRunResultDocument>.Update
                    .SetOnInsert(d => d.Id, Guid.NewGuid())
                    .SetOnInsert(d => d.RunId, runId)
                    .Set(d => d.ServiceName, group.Key.ServiceName)
                    .Set(d => d.EndpointKey, result.EndpointKey)
                    .Set(d => d.StartedAt, startedAt)
                    .Set(d => d.Result, result);

                await _resultCollection.UpdateOneAsync(
                    resultFilter,
                    resultUpdate,
                    new UpdateOptions { IsUpsert = true },
                    ct);
            }
        }
    }

    private sealed class RunServiceGroupKeyComparer : IEqualityComparer<(Guid RunId, string ServiceName)>
    {
        public static readonly RunServiceGroupKeyComparer Instance = new();

        public bool Equals((Guid RunId, string ServiceName) x, (Guid RunId, string ServiceName) y)
            => x.RunId == y.RunId
               && string.Equals(x.ServiceName, y.ServiceName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid RunId, string ServiceName) obj)
            => HashCode.Combine(obj.RunId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ServiceName));
    }

    public async Task SaveServiceRunStateAsync(
        Guid runId,
        string runMode,
        IEnumerable<string> serviceNames,
        DateTimeOffset startedAt,
        CancellationToken ct = default)
    {
        var normalizedMode = string.Equals(runMode, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "Single";
        var services = serviceNames
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var serviceName in services)
        {
            var filter = Builders<ApiHealthRunDocument>.Filter.And(
                Builders<ApiHealthRunDocument>.Filter.Eq(d => d.RunId, runId),
                Builders<ApiHealthRunDocument>.Filter.Eq(d => d.ServiceName, serviceName));

            var update = Builders<ApiHealthRunDocument>.Update
                .SetOnInsert(d => d.Id, Guid.NewGuid())
                .SetOnInsert(d => d.RunId, runId)
                .SetOnInsert(d => d.ServiceName, serviceName)
                .Set(d => d.RunMode, normalizedMode)
                .Set(d => d.StartedAt, startedAt)
                .SetOnInsert(d => d.EndpointResults, []);

            await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }
    }

    public async Task<ApiHealthLatestRunContext?> GetLatestRunContextAsync(CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(FilterDefinition<ApiHealthRunDocument>.Empty)
            .SortByDescending(d => d.StartedAt)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        if (doc == null)
            return null;

        return new ApiHealthLatestRunContext
        {
            RunId = doc.RunId,
            RunMode = string.Equals(doc.RunMode, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "Single",
            ServiceName = doc.ServiceName,
            StartedAt = doc.StartedAt
        };
    }

    public async Task<Dictionary<string, ApiTestRunResult>> GetLatestResultsByServiceAsync(
    IEnumerable<string> endpointKeys,
    CancellationToken ct = default)
    {
        var keys = endpointKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (keys.Count == 0)
            return new();

        // api_health_runs remains the lightweight source of run/service metadata.
        // Select only the latest run for each service in Mongo rather than loading
        // every historical endpoint result into memory.
        var latestRunDocs = await _collection
            .Aggregate()
            .SortByDescending(d => d.StartedAt)
            .Group(
                d => d.ServiceName,
                g => g.First())
            .ToListAsync(ct);

        if (latestRunDocs.Count == 0)
            return new();

        var latestRunFilters = latestRunDocs
            .Select(d =>
                Builders<ApiHealthRunResultDocument>.Filter.And(
                    Builders<ApiHealthRunResultDocument>.Filter.Eq(
                        r => r.RunId,
                        d.RunId),
                    Builders<ApiHealthRunResultDocument>.Filter.Eq(
                        r => r.ServiceName,
                        d.ServiceName)))
            .ToList();

        var resultFilter =
            Builders<ApiHealthRunResultDocument>.Filter.And(
                Builders<ApiHealthRunResultDocument>.Filter.In(
                    r => r.EndpointKey,
                    keys),
                Builders<ApiHealthRunResultDocument>.Filter.Or(
                    latestRunFilters));

        var resultDocs = await _resultCollection
            .Find(resultFilter)
            .ToListAsync(ct);

        var results = resultDocs
            .Select(d => d.Result)
            .ToList();

        // The latest service run may still be a legacy document created before
        // endpoint results were moved to api_health_run_results.
        results.AddRange(
            latestRunDocs
                .SelectMany(d => d.EndpointResults)
                .Where(r => keys.Contains(
                    r.EndpointKey,
                    StringComparer.Ordinal)));

        return results
            .GroupBy(r => r.EndpointKey, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.ExecutedAt).First(),
                StringComparer.Ordinal);
    }

    public async Task<Dictionary<string, ApiTestRunResult>> GetLatestResultsForRunAsync(
    Guid runId,
    IEnumerable<string> endpointKeys,
    CancellationToken ct = default)
    {
        var keys = endpointKeys.ToHashSet(StringComparer.Ordinal);
        if (keys.Count == 0)
            return new();

        // New separately persisted results.
        var resultDocs = await _resultCollection
            .Find(d => d.RunId == runId && keys.Contains(d.EndpointKey))
            .ToListAsync(ct);

        var results = resultDocs
            .Select(d => d.Result)
            .ToList();

        // Legacy results retained for API Health history created before
        // endpoint results were moved to their own collection.
        var legacyDocs = await _collection
            .Find(d => d.RunId == runId)
            .ToListAsync(ct);

        results.AddRange(
            legacyDocs
                .SelectMany(d => d.EndpointResults)
                .Where(r => keys.Contains(r.EndpointKey)));

        return results
            .GroupBy(r => r.EndpointKey, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.ExecutedAt).First(),
                StringComparer.Ordinal);
    }

    public async Task<ApiTestRunHistoryPage> GetHistoryAsync(
    string endpointKey,
    int pageNumber,
    int pageSize,
    CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, pageSize);

        var skip = (pageNumber - 1) * pageSize;
        var requiredCount = skip + pageSize;

        var resultFilter =
            Builders<ApiHealthRunResultDocument>.Filter.Eq(
                d => d.EndpointKey,
                endpointKey);

        var legacyFilter =
            Builders<ApiHealthRunDocument>.Filter.ElemMatch(
                d => d.EndpointResults,
                r => r.EndpointKey == endpointKey);

        // Retrieve only RunIds for the total count so full request/response
        // payloads are not loaded just to calculate pagination metadata.
        var newRunIds = await _resultCollection
            .Find(resultFilter)
            .Project(d => d.RunId)
            .ToListAsync(ct);

        var legacyRunIds = await _collection
            .Find(legacyFilter)
            .Project(d => d.RunId)
            .ToListAsync(ct);

        var totalCount = newRunIds
            .Concat(legacyRunIds)
            .Distinct()
            .Count();

        // Mongo performs the indexed filter/sort and bounds the number of
        // full result documents returned. We fetch at most enough from each
        // source to construct the requested combined page.
        var resultDocs = await _resultCollection
            .Find(resultFilter)
            .SortByDescending(d => d.StartedAt)
            .Limit(requiredCount)
            .ToListAsync(ct);

        var legacyDocs = await _collection
            .Find(legacyFilter)
            .SortByDescending(d => d.StartedAt)
            .Limit(requiredCount)
            .ToListAsync(ct);

        var newResults = resultDocs
            .Select(d => d.Result);

        var legacyResults = legacyDocs
            .SelectMany(d => d.EndpointResults)
            .Where(r => string.Equals(
                r.EndpointKey,
                endpointKey,
                StringComparison.Ordinal));

        var pagedRuns = newResults
            .Concat(legacyResults)
            .GroupBy(r => r.RunId)
            .Select(g => g
                .OrderByDescending(r => r.ExecutedAt)
                .First())
            .OrderByDescending(r => r.ExecutedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new ApiTestRunHistoryPage
        {
            EndpointKey = endpointKey,
            Runs = pagedRuns,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task UpsertExecutionRunStatusAsync(ApiHealthExecutionRunStatus status, CancellationToken ct = default)
    {
        var filter = Builders<ApiHealthExecutionRunDocument>.Filter.Eq(d => d.RunId, status.RunId);
        var update = Builders<ApiHealthExecutionRunDocument>.Update
            .SetOnInsert(d => d.RunId, status.RunId)
            .Set(d => d.SeedRunId, status.SeedRunId)
            .Set(d => d.SeedRunName, status.SeedRunName)
            .Set(d => d.Scope, status.Scope)
            .Set(d => d.ServiceName, status.ServiceName)
            .Set(d => d.StartedAt, status.StartedAt)
            .Set(d => d.Phase, status.Phase)
            .Set(d => d.Message, status.Message)
            .Set(d => d.IsError, status.IsError)
            .Set(d => d.IsCompleted, status.IsCompleted)
            .Set(d => d.Failed, status.Failed)
            .Set(d => d.Error, status.Error)
            .Set(d => d.FinishedAt, status.FinishedAt)
            .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow);

        return _executionCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    public Task AttachSeedRunAsync(Guid runId, Guid seedRunId, string? seedRunName, CancellationToken ct = default)
    {
        var filter = Builders<ApiHealthExecutionRunDocument>.Filter.Eq(d => d.RunId, runId);
        var update = Builders<ApiHealthExecutionRunDocument>.Update
            .Set(d => d.SeedRunId, seedRunId)
            .Set(d => d.SeedRunName, seedRunName)
            .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow);

        return _executionCollection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<ApiHealthExecutionRunStatus?> GetActiveExecutionRunStatusAsync(CancellationToken ct = default)
    {
        var doc = await _executionCollection
            .Find(d => !d.IsCompleted)
            .SortByDescending(d => d.StartedAt)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return doc == null ? null : ToExecutionStatus(doc);
    }

    public async Task<ApiHealthExecutionRunStatus?> GetLatestExecutionRunStatusAsync(CancellationToken ct = default)
    {
        var doc = await _executionCollection
            .Find(FilterDefinition<ApiHealthExecutionRunDocument>.Empty)
            .SortByDescending(d => d.StartedAt)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return doc == null ? null : ToExecutionStatus(doc);
    }

    public async Task<ApiHealthExecutionRunStatus?> GetExecutionRunStatusAsync(Guid runId, CancellationToken ct = default)
    {
        var doc = await _executionCollection
            .Find(d => d.RunId == runId)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return doc == null ? null : ToExecutionStatus(doc);
    }

    public Task CompleteExecutionRunAsync(Guid runId, bool failed, string? error, DateTimeOffset finishedAt, CancellationToken ct = default)
    {
        var filter = Builders<ApiHealthExecutionRunDocument>.Filter.Eq(d => d.RunId, runId);
        var update = Builders<ApiHealthExecutionRunDocument>.Update
            .Set(d => d.IsCompleted, true)
            .Set(d => d.Failed, failed)
            .Set(d => d.Error, error)
            .Set(d => d.FinishedAt, finishedAt)
            .Set(d => d.UpdatedAt, DateTimeOffset.UtcNow);

        return _executionCollection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private static ApiHealthExecutionRunStatus ToExecutionStatus(ApiHealthExecutionRunDocument d) => new()
    {
        RunId = d.RunId,
        SeedRunId = d.SeedRunId,
        SeedRunName = d.SeedRunName,
        Scope = d.Scope,
        ServiceName = d.ServiceName,
        StartedAt = d.StartedAt,
        Phase = d.Phase,
        Message = d.Message,
        IsError = d.IsError,
        IsCompleted = d.IsCompleted,
        Failed = d.Failed,
        Error = d.Error,
        FinishedAt = d.FinishedAt
    };
}
