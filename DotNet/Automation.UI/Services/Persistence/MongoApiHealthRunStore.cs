using Automation.UI.Models.ApiHealth;
using MongoDB.Driver;

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
}

public sealed class MongoApiHealthRunStore : IApiHealthRunStore
{
    private readonly IMongoCollection<ApiHealthRunDocument> _collection;

    public MongoApiHealthRunStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<ApiHealthRunDocument>("api_health_runs");
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
        ExecutedAt = r.ExecutedAt,
        DurationMs = r.DurationMs,
        RequestUrl = r.RequestUrl,
        RequestMethod = r.RequestMethod,
        TraceId = r.TraceId,
        ResponseBody = r.ResponseBody
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
        ExecutedAt = d.ExecutedAt,
        DurationMs = d.DurationMs,
        RequestUrl = d.RequestUrl,
        RequestMethod = d.RequestMethod,
        TraceId = d.TraceId,
        ResponseBody = d.ResponseBody
    };
}
