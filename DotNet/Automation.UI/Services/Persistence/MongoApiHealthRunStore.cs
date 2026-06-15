using Automation.UI.Models.ApiHealth;
using MongoDB.Driver;
using Automation.UI.Models.ApiHealth;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Provides MongoDB persistence for API health test run results.
/// </summary>
public interface IApiHealthRunStore
{
    Task SaveRunResultAsync(ApiTestRunResult result, CancellationToken ct = default);
    Task SaveRunResultsAsync(IEnumerable<ApiTestRunResult> results, CancellationToken ct = default);
    Task<ApiTestRunResult?> GetLatestResultAsync(string endpointKey, CancellationToken ct = default);
    Task<Dictionary<string, ApiTestRunResult>> GetLatestResultsAsync(IEnumerable<string> endpointKeys, CancellationToken ct = default);
    Task<ApiTestRunHistoryPage> GetHistoryAsync(string endpointKey, int pageNumber, int pageSize, CancellationToken ct = default);
    Task UpsertExecutionRunStatusAsync(ApiHealthExecutionRunStatus status, CancellationToken ct = default);
    Task AttachSeedRunAsync(Guid runId, Guid seedRunId, string? seedRunName, CancellationToken ct = default);
    Task<ApiHealthExecutionRunStatus?> GetActiveExecutionRunStatusAsync(CancellationToken ct = default);
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

    public async Task SaveRunResultAsync(ApiTestRunResult result, CancellationToken ct = default)
    {
        var doc = ToDocument(result);
        await _collection.InsertOneAsync(doc, cancellationToken: ct);
    }

    public async Task SaveRunResultsAsync(IEnumerable<ApiTestRunResult> results, CancellationToken ct = default)
    {
        var docs = results.Select(ToDocument).ToList();
        if (docs.Count > 0)
            await _collection.InsertManyAsync(docs, cancellationToken: ct);
    }

    public async Task<ApiTestRunResult?> GetLatestResultAsync(string endpointKey, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(d => d.EndpointKey == endpointKey)
            .SortByDescending(d => d.ExecutedAt)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return doc == null ? null : FromDocument(doc);
    }

    public async Task<Dictionary<string, ApiTestRunResult>> GetLatestResultsAsync(
        IEnumerable<string> endpointKeys, CancellationToken ct = default)
    {
        var keys = endpointKeys.ToList();
        if (keys.Count == 0) return new();

        // Aggregate: group by EndpointKey, take the latest ExecutedAt per group.
        var pipeline = _collection.Aggregate()
            .Match(Builders<ApiHealthRunDocument>.Filter.In(d => d.EndpointKey, keys))
            .SortByDescending(d => d.ExecutedAt)
            .Group(
                d => d.EndpointKey,
                g => new { Key = g.Key, Doc = g.First() });

        var groups = await pipeline.ToListAsync(ct);
        return groups.ToDictionary(g => g.Key, g => FromDocument(g.Doc));
    }

    public async Task<ApiTestRunHistoryPage> GetHistoryAsync(
        string endpointKey, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var filter = Builders<ApiHealthRunDocument>.Filter.Eq(d => d.EndpointKey, endpointKey);
        var totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var docs = await _collection
            .Find(filter)
            .SortByDescending(d => d.ExecutedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return new ApiTestRunHistoryPage
        {
            EndpointKey = endpointKey,
            Runs = docs.Select(FromDocument).ToList(),
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

    private static ApiHealthRunDocument ToDocument(ApiTestRunResult r) => new()
    {
        Id = r.Id,
        EndpointKey = r.EndpointKey,
        ServiceName = r.ServiceName,
        EndpointName = r.EndpointName,
        Passed = r.Passed,
        Skipped = r.Skipped,
        SkipReason = r.SkipReason,
        ActualStatusCode = r.ActualStatusCode,
        ExpectedStatusCode = r.ExpectedStatusCode,
        ErrorMessage = r.ErrorMessage,
        ResponseSnippet = r.ResponseSnippet,
        RequestUrl = r.RequestUrl,
        RequestMethod = r.RequestMethod,
        RequestBody = r.RequestBody,
        TraceId = r.TraceId,
        ResponseBody = r.ResponseBody,
        ExecutedAt = r.ExecutedAt,
        DurationMs = r.DurationMs
    };

    private static ApiTestRunResult FromDocument(ApiHealthRunDocument d) => new()
    {
        Id = d.Id,
        EndpointKey = d.EndpointKey,
        ServiceName = d.ServiceName,
        EndpointName = d.EndpointName,
        Passed = d.Passed,
        Skipped = d.Skipped,
        SkipReason = d.SkipReason,
        ActualStatusCode = d.ActualStatusCode,
        ExpectedStatusCode = d.ExpectedStatusCode,
        ErrorMessage = d.ErrorMessage,
        ResponseSnippet = d.ResponseSnippet,
        RequestUrl = d.RequestUrl,
        RequestMethod = d.RequestMethod,
        RequestBody = d.RequestBody,
        TraceId = d.TraceId,
        ResponseBody = d.ResponseBody,
        ExecutedAt = d.ExecutedAt,
        DurationMs = d.DurationMs
    };

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
