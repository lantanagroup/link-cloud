using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services.TestRail;

public sealed class TestRailApiClient : ITestRailApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(3);

    private readonly Func<HttpClient> _httpFactory;
    private readonly TestRailOptions _options;
    private readonly ILogger<TestRailApiClient> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public TestRailApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<TestRailOptions> options,
        ILogger<TestRailApiClient> logger)
        : this(() => httpClientFactory.CreateClient("TestRail"), options.Value, logger)
    {
    }

    public TestRailApiClient(HttpClient http, TestRailOptions options, ILogger<TestRailApiClient> logger)
        : this(() => http, options, logger)
    {
    }

    internal TestRailApiClient(
        Func<HttpClient> httpFactory,
        TestRailOptions options,
        ILogger<TestRailApiClient> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    public async Task<int> AddRunAsync(
        int projectId,
        int suiteId,
        string name,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken = default)
    {
        var payload = new AddRunRequest
        {
            SuiteId = suiteId,
            Name = name,
            IncludeAll = false,
            CaseIds = caseIds.ToList()
        };

        var response = await SendJsonAsync<AddRunResponse>(
            HttpMethod.Post,
            $"add_run/{projectId}",
            payload,
            cancellationToken);

        if (response is null || response.Id <= 0)
            throw new InvalidOperationException("TestRail add_run returned no run id.");

        _logger.LogDebug("TestRail add_run created run {TestRailRunId} in project {ProjectId}.", response.Id, projectId);
        return response.Id;
    }

    public async Task UpdateRunCaseIdsAsync(
        int runId,
        IReadOnlyList<int> caseIds,
        CancellationToken cancellationToken = default)
    {
        var payload = new UpdateRunCasesRequest
        {
            IncludeAll = false,
            CaseIds = caseIds.ToList()
        };

        await SendJsonAsync<AddRunResponse>(
            HttpMethod.Post,
            $"update_run/{runId}",
            payload,
            cancellationToken);
    }

    public async Task<IReadOnlyList<TestRailResultDto>> AddResultsForCasesAsync(
        int runId,
        IReadOnlyList<TestRailCaseResult> results,
        CancellationToken cancellationToken = default)
    {
        var payload = new AddResultsRequest
        {
            Results = results.Select(r => new AddResultItem
            {
                CaseId = r.CaseId,
                StatusId = r.StatusId,
                Comment = r.Comment,
                Elapsed = r.Elapsed
            }).ToList()
        };

        var response = await SendJsonAsync<List<AddResultResponse>>(
            HttpMethod.Post,
            $"add_results_for_cases/{runId}",
            payload,
            cancellationToken);

        if (response is null)
            return [];

        // TestRail returns one result per posted item, in the order posted, with id and test_id
        // but no case_id. Take the case id from the posted item at the same position.
        var sameShape = response.Count == results.Count;
        return response
            .Select((r, index) => new TestRailResultDto
            {
                Id = r.Id,
                TestId = r.TestId,
                CaseId = r.CaseId is > 0
                    ? r.CaseId.Value
                    : sameShape ? results[index].CaseId : 0
            })
            .ToList();
    }

    public async Task AddAttachmentToResultAsync(
        int resultId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(() =>
        {
            var request = CreateRequest(HttpMethod.Post, $"add_attachment_to_result/{resultId}");
            var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "attachment", fileName);
            request.Content = form;
            return request;
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                FormatFailure($"add_attachment_to_result/{resultId}", response.StatusCode, body));
        }
    }

    internal const int MaxProjectPages = 40;

    public async Task<IReadOnlyList<TestRailNamedItem>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<TestRailNamedItem>();
        var offset = 0;
        for (var page = 0; page < MaxProjectPages; page++)
        {
            var path = offset == 0 ? "get_projects" : $"get_projects&offset={offset}";
            using var doc = await GetJsonAsync(path, cancellationToken);
            var (pageItems, hasNext) = ReadNamedItems(doc.RootElement, "projects");
            items.AddRange(pageItems);
            if (!hasNext || pageItems.Count == 0)
                break;
            offset += pageItems.Count;
        }

        return items;
    }

    public async Task<IReadOnlyList<TestRailNamedItem>> GetSuitesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var items = new List<TestRailNamedItem>();
        var offset = 0;
        for (var page = 0; page < MaxProjectPages; page++)
        {
            var path = offset == 0 ? $"get_suites/{projectId}" : $"get_suites/{projectId}&offset={offset}";
            using var doc = await GetJsonAsync(path, cancellationToken);
            var (pageItems, hasNext) = ReadNamedItems(doc.RootElement, "suites");
            items.AddRange(pageItems);
            if (!hasNext || pageItems.Count == 0)
                break;
            offset += pageItems.Count;
        }

        return items;
    }

    private async Task<JsonDocument> GetJsonAsync(string apiPath, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(() => CreateRequest(HttpMethod.Get, apiPath), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatFailure(apiPath, response.StatusCode, body));

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "[]" : body);
    }

    /// <summary>
    /// Reads a TestRail list. Newer TestRail versions wrap lists in an object with a
    /// <c>_links.next</c> link; older ones return a bare array.
    /// </summary>
    private static (List<TestRailNamedItem> Items, bool HasNext) ReadNamedItems(JsonElement root, string listProperty)
    {
        var list = root;
        var hasNext = false;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (!root.TryGetProperty(listProperty, out list))
                return ([], false);
            hasNext = root.TryGetProperty("_links", out var links)
                && links.ValueKind == JsonValueKind.Object
                && links.TryGetProperty("next", out var next)
                && next.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(next.GetString());
        }

        var items = new List<TestRailNamedItem>();
        if (list.ValueKind != JsonValueKind.Array)
            return (items, false);

        foreach (var element in list.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("id", out var id)
                || !id.TryGetInt32(out var idValue))
            {
                continue;
            }

            var name = element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? string.Empty
                : string.Empty;
            var completed = element.TryGetProperty("is_completed", out var c)
                && (c.ValueKind == JsonValueKind.True || (c.ValueKind == JsonValueKind.Number && c.GetInt32() != 0));
            items.Add(new TestRailNamedItem { Id = idValue, Name = name, IsCompleted = completed });
        }

        return (items, hasNext);
    }

    private async Task<T?> SendJsonAsync<T>(
        HttpMethod method,
        string apiPath,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var response = await SendWithRetryAsync(() =>
        {
            var request = CreateRequest(method, apiPath);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatFailure(apiPath, response.StatusCode, body));

        if (string.IsNullOrWhiteSpace(body))
            return default;

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    /// <summary>
    /// Keeps the status and the API operation. TestRail's own errors are a short JSON
    /// message. A wrong host can return a page, so the body is capped, and the API key
    /// (and the Basic token built from it) is removed before this text is logged.
    /// </summary>
    private string FormatFailure(string apiPath, HttpStatusCode status, string? body)
    {
        var text = body ?? string.Empty;
        if (_options.Username.Length > 0 && _options.ApiKey.Length > 0)
        {
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.Username}:{_options.ApiKey}"));
            text = text.Replace(token, "[redacted]", StringComparison.Ordinal);
        }

        if (_options.ApiKey.Length >= 8)
            text = text.Replace(_options.ApiKey, "[redacted]", StringComparison.Ordinal);

        text = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        if (text.Length > MaxFailureBodyChars)
            text = string.Concat(text.AsSpan(0, MaxFailureBodyChars), "...");

        return $"TestRail {apiPath} failed with {(int)status}: {text}";
    }

    internal const int MaxFailureBodyChars = 500;

    private HttpRequestMessage CreateRequest(HttpMethod method, string apiPath)
    {
        var uri = BuildUri(apiPath);
        var request = new HttpRequestMessage(method, uri);
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.Username}:{_options.ApiKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private Uri BuildUri(string apiPath)
    {
        var baseUrl = _options.BaseUrl.Trim().TrimEnd('/');
        return new Uri($"{baseUrl}/index.php?/api/v2/{apiPath}");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        var response = await SendCoreAsync(createRequest(), cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
            return response;

        var delay = RetryDelay(response);
        response.Dispose();
        _logger.LogWarning(
            "TestRail returned HTTP 429. Retrying once after {RetryAfterMilliseconds} ms.",
            (int)delay.TotalMilliseconds);

        await _delay(delay, cancellationToken);

        var retried = await SendCoreAsync(createRequest(), cancellationToken);
        if (retried.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            _logger.LogWarning("TestRail returned HTTP 429 again. Giving up on this call.");

        return retried;
    }

    private static TimeSpan RetryDelay(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        TimeSpan delay;
        if (header?.Delta is TimeSpan delta)
            delay = delta;
        else if (header?.Date is DateTimeOffset retryAt)
            delay = retryAt - DateTimeOffset.UtcNow;
        else
            delay = TimeSpan.Zero;

        if (delay < TimeSpan.Zero)
            return TimeSpan.Zero;
        return delay > MaxRetryAfter ? MaxRetryAfter : delay;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpFactory();
            return await client.SendAsync(request, cancellationToken);
        }
        finally
        {
            request.Dispose();
        }
    }

    private sealed class AddRunRequest
    {
        [JsonPropertyName("suite_id")]
        public int SuiteId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("include_all")]
        public bool IncludeAll { get; set; }

        [JsonPropertyName("case_ids")]
        public List<int> CaseIds { get; set; } = [];
    }

    private sealed class UpdateRunCasesRequest
    {
        [JsonPropertyName("include_all")]
        public bool IncludeAll { get; set; }

        [JsonPropertyName("case_ids")]
        public List<int> CaseIds { get; set; } = [];
    }

    private sealed class AddRunResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    private sealed class AddResultsRequest
    {
        [JsonPropertyName("results")]
        public List<AddResultItem> Results { get; set; } = [];
    }

    private sealed class AddResultItem
    {
        [JsonPropertyName("case_id")]
        public int CaseId { get; set; }

        [JsonPropertyName("status_id")]
        public int StatusId { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("elapsed")]
        public string? Elapsed { get; set; }
    }

    private sealed class AddResultResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("test_id")]
        public int TestId { get; set; }

        [JsonPropertyName("case_id")]
        public int? CaseId { get; set; }
    }
}
