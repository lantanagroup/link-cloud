using Automation.UI.Models.ApiHealth;
using MongoDB.Driver;
using Automation.UI.Models.ApiHealth;

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

    public MongoApiHealthRunStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<ApiHealthRunDocument>("api_health_runs");
        _executionCollection = database.GetCollection<ApiHealthExecutionRunDocument>("api_health_execution_runs");
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
            .GroupBy(r => r.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in resultGroups)
        {
            var runId = group.Select(r => r.RunId).FirstOrDefault(id => id != Guid.Empty);
            if (runId == Guid.Empty)
                continue;

            var filter = Builders<ApiHealthRunDocument>.Filter.And(
                Builders<ApiHealthRunDocument>.Filter.Eq(d => d.RunId, runId),
                Builders<ApiHealthRunDocument>.Filter.Eq(d => d.ServiceName, group.Key));

            var existing = await _collection.Find(filter).Limit(1).FirstOrDefaultAsync(ct);
            var merged = (existing?.EndpointResults ?? [])
                .ToDictionary(r => r.EndpointKey, StringComparer.Ordinal);

            foreach (var result in group)
                merged[result.EndpointKey] = result;

            var doc = new ApiHealthRunDocument
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                RunId = runId,
                ServiceName = group.Key,
                RunMode = normalizedMode,
                StartedAt = startedAt,
                EndpointResults = merged.Values
                    .OrderBy(r => r.EndpointKey, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
        }
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
        var keys = endpointKeys.ToHashSet(StringComparer.Ordinal);
        if (keys.Count == 0) return new();

        var docs = await _collection
            .Find(d => d.EndpointResults.Any(r => keys.Contains(r.EndpointKey)))
            .SortByDescending(d => d.StartedAt)
            .ToListAsync(ct);

        var latestByService = new Dictionary<string, ApiHealthRunDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in docs)
        {
            if (!latestByService.ContainsKey(doc.ServiceName))
                latestByService[doc.ServiceName] = doc;
        }

        return latestByService.Values
            .SelectMany(d => d.EndpointResults)
            .Where(r => keys.Contains(r.EndpointKey))
            .GroupBy(r => r.EndpointKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ExecutedAt).First(), StringComparer.Ordinal);
    }

    public async Task<Dictionary<string, ApiTestRunResult>> GetLatestResultsForRunAsync(
        Guid runId,
        IEnumerable<string> endpointKeys,
        CancellationToken ct = default)
    {
        var keys = endpointKeys.ToHashSet(StringComparer.Ordinal);
        if (keys.Count == 0) return new();

        var docs = await _collection
            .Find(d => d.RunId == runId)
            .ToListAsync(ct);

        return docs
            .SelectMany(d => d.EndpointResults)
            .Where(r => keys.Contains(r.EndpointKey))
            .GroupBy(r => r.EndpointKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ExecutedAt).First(), StringComparer.Ordinal);
    }

    public async Task<ApiTestRunHistoryPage> GetHistoryAsync(
        string endpointKey, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var docs = await _collection
            .Find(d => d.EndpointResults.Any(r => r.EndpointKey == endpointKey))
            .SortByDescending(d => d.StartedAt)
            .ToListAsync(ct);

        var allRuns = docs
            .SelectMany(d => d.EndpointResults)
            .Where(r => string.Equals(r.EndpointKey, endpointKey, StringComparison.Ordinal))
            .OrderByDescending(r => r.ExecutedAt)
            .ToList();

        var totalCount = (long)allRuns.Count;
        var pagedRuns = allRuns
            .Skip((pageNumber - 1) * pageSize)
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
