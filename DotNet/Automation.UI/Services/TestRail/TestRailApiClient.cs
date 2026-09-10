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

    private readonly Func<HttpClient> _httpFactory;
    private readonly TestRailOptions _options;
    private readonly ILogger<TestRailApiClient> _logger;

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

    internal TestRailApiClient(Func<HttpClient> httpFactory, TestRailOptions options, ILogger<TestRailApiClient> logger)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
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

        return response
            .Select(r => new TestRailResultDto { Id = r.Id, CaseId = r.CaseId })
            .ToList();
    }

    public async Task AddAttachmentToResultAsync(
        int resultId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"add_attachment_to_result/{resultId}");
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "attachment", fileName);
        request.Content = form;

        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"TestRail add_attachment_to_result failed with {(int)response.StatusCode}: {body}");
        }
    }

    private async Task<T?> SendJsonAsync<T>(
        HttpMethod method,
        string apiPath,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, apiPath);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"TestRail {apiPath} failed with {(int)response.StatusCode}: {body}");
        }

        if (string.IsNullOrWhiteSpace(body))
            return default;

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

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

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpFactory();
        return await client.SendAsync(request, cancellationToken);
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

        [JsonPropertyName("case_id")]
        public int CaseId { get; set; }
    }
}
