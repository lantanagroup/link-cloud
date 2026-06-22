using System.Diagnostics;
using System.Text.Json;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using LantanaGroup.Link.Sdk.ApiClient;

namespace Automation.UI.Services.ApiHealth.TestSuites;

/// <summary>
/// Shared helpers for running individual test steps and tracking results.
/// </summary>
public abstract class ServiceTestSuiteBase : IServiceTestSuite
{
    public abstract string ServiceName { get; }

    public abstract IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions();
    public virtual IReadOnlyList<ApiHealthSeedRequirement> GetSeedRequirements() => [];
    public abstract Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs a step that performs custom logic (e.g., inspecting response bodies, multi-call sequences).
    /// The action is expected to throw if the step fails.
    /// </summary>
    protected async Task<ApiTestRunResult> RunStepAsync(
        string endpointName,
        Func<Task> action,
        int expectedStatusCode = 200,
        CancellationToken ct = default)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = expectedStatusCode,
            ExecutedAt = DateTimeOffset.UtcNow
        };

        var sw = Stopwatch.StartNew();
        try
        {
            await action();
            sw.Stop();
            result.Passed = true;
            result.ActualStatusCode = expectedStatusCode;
            result.DurationMs = sw.ElapsedMilliseconds;
            result.RequestBody = NoRequestBodyNote();
            result.ResponseBody = NoResponseBodyNote(expectedStatusCode);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = ex.Message;
            result.RequestBody = NoRequestBodyNote();
            result.ResponseBody = Truncate(ex.ToString());
        }

        return result;
    }

    /// <summary>
    /// Runs a step that returns a LinkApiResponse. Validates the actual status code against the expected one.
    /// This is the primary helper for testing specific HTTP response code paths.
    /// </summary>
    protected async Task<ApiTestRunResult> RunStepAsync(
        string endpointName,
        int expectedStatusCode,
        Func<Task<LinkApiResponse>> action,
        CancellationToken ct = default)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = expectedStatusCode,
            ExecutedAt = DateTimeOffset.UtcNow
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await action();
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.ActualStatusCode = response.StatusCode;
            result.Passed = response.StatusCode == expectedStatusCode;
            result.RequestUrl = response.RequestUrl;
            result.RequestMethod = response.RequestMethod;
            result.RequestBody = BodyOrNote(response.RequestBody, NoRequestBodyNote(response.RequestMethod));
            result.TraceId = response.TraceId;
            result.ResponseBody = BodyOrNote(response.RawBody, NoResponseBodyNote(response.StatusCode));
            if (!result.Passed)
                result.ErrorMessage = BuildStatusMismatchMessage(
                    $"Expected HTTP {expectedStatusCode}",
                    response.StatusCode,
                    response.RawBody,
                    response.TraceId,
                    includeBodyForNon500: true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = ex.Message;
            result.RequestBody = NoRequestBodyNote();
            result.ResponseBody = Truncate(ex.ToString());
        }

        return result;
    }

    /// <summary>
    /// Runs a step that returns a typed LinkApiResponse. Validates the status code.
    /// </summary>
    protected async Task<ApiTestRunResult> RunStepAsync<T>(
        string endpointName,
        int expectedStatusCode,
        Func<Task<LinkApiResponse<T>>> action,
        CancellationToken ct = default) where T : class
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = expectedStatusCode,
            ExecutedAt = DateTimeOffset.UtcNow
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await action();
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.ActualStatusCode = response.StatusCode;
            result.Passed = response.StatusCode == expectedStatusCode;
            result.RequestUrl = response.RequestUrl;
            result.RequestMethod = response.RequestMethod;
            result.RequestBody = BodyOrNote(response.RequestBody, NoRequestBodyNote(response.RequestMethod));
            result.TraceId = response.TraceId;
            result.ResponseBody = BodyOrNote(response.RawBody, NoResponseBodyNote(response.StatusCode));
            if (!result.Passed)
                result.ErrorMessage = BuildStatusMismatchMessage(
                    $"Expected HTTP {expectedStatusCode}",
                    response.StatusCode,
                    response.RawBody,
                    response.TraceId,
                    includeBodyForNon500: true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = ex.Message;
            result.RequestBody = NoRequestBodyNote();
            result.ResponseBody = Truncate(ex.ToString());
        }

        return result;
    }

    /// <summary>
    /// Runs a step that accepts any one of several status codes as a pass.
    /// Use when the response code legitimately varies by environment configuration
    /// (e.g. a facility-exists check that can be disabled).
    /// On pass the ExpectedStatusCode is set to the actual code so the status
    /// column on the dashboard shows a clean match regardless of which code fired.
    /// </summary>
    protected async Task<ApiTestRunResult> RunStepAsync(
        string endpointName,
        int[] acceptedStatusCodes,
        Func<Task<LinkApiResponse>> action,
        CancellationToken ct = default)
    {
        var result = new ApiTestRunResult
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            ExpectedStatusCode = acceptedStatusCodes[0],
            ExecutedAt = DateTimeOffset.UtcNow
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await action();
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.ActualStatusCode = response.StatusCode;
            result.Passed = acceptedStatusCodes.Contains(response.StatusCode);
            result.RequestUrl = response.RequestUrl;
            result.RequestMethod = response.RequestMethod;
            result.RequestBody = BodyOrNote(response.RequestBody, NoRequestBodyNote(response.RequestMethod));
            result.TraceId = response.TraceId;
            result.ResponseBody = BodyOrNote(response.RawBody, NoResponseBodyNote(response.StatusCode));
            if (result.Passed)
                result.ExpectedStatusCode = response.StatusCode;
            else
                result.ErrorMessage = BuildStatusMismatchMessage(
                    $"Expected one of [{string.Join(", ", acceptedStatusCodes)}]",
                    response.StatusCode,
                    response.RawBody,
                    response.TraceId,
                    includeBodyForNon500: true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = ex.Message;
            result.RequestBody = NoRequestBodyNote();
            result.ResponseBody = Truncate(ex.ToString());
        }

        return result;
    }

    /// <summary>
    /// Produces a skipped result for a step that cannot be executed in this environment.
    /// Skipped steps are neither passed nor failed — they are shown distinctly on the dashboard.
    /// </summary>
    protected ApiTestRunResult SkipStepAsync(string endpointName, string skipReason) =>
        new()
        {
            EndpointKey = $"{ServiceName}::{endpointName}",
            ServiceName = ServiceName,
            EndpointName = endpointName,
            Skipped = true,
            SkipReason = skipReason,
            RequestBody = "Request was not sent because this test step was skipped.",
            ResponseBody = "Response was not received because this test step was skipped.",
            ExecutedAt = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// Creates an <see cref="ApiEndpointDefinition"/> for a step that is permanently
    /// skipped. The <paramref name="skipReason"/> is surfaced on the dashboard both
    /// before and after the suite runs.
    /// </summary>
    protected ApiEndpointDefinition SkipStep(
        string name,
        string description,
        string skipReason,
        string? group = null) => new()
    {
        ServiceName = ServiceName,
        GroupName = group,
        EndpointName = name,
        Description = description,
        AlwaysSkipReason = skipReason,
        IsTestSuiteStep = true
    };

    /// <summary>
    /// Executes an action ignoring any errors (used for cleanup).
    /// </summary>
    protected static async Task TryCleanupAsync(Func<Task> action)
    {
        try { await action(); }
        catch { /* best effort cleanup */ }
    }

    private static string Truncate(string? value, int maxLength = 300) =>
        value == null ? "" : (value.Length > maxLength ? value[..maxLength] : value);

    private static string BodyOrNote(string? value, string note)
    {
        var body = Truncate(value);
        return string.IsNullOrWhiteSpace(body) ? note : body;
    }

    private static string NoRequestBodyNote(string? requestMethod = null)
    {
        var method = requestMethod?.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(method)
            ? "No request body was sent."
            : $"No request body was sent ({method}).";
    }

    private static string NoResponseBodyNote(int? statusCode = null)
    {
        if (statusCode == 204)
            return "No response body was returned (204 No Content).";

        return statusCode.HasValue
            ? $"No response body was returned (HTTP {statusCode.Value})."
            : "No response body was returned.";
    }

    private static string BuildStatusMismatchMessage(
        string expectedText,
        int actualStatusCode,
        string? rawBody,
        string? traceId,
        bool includeBodyForNon500)
    {
        var baseMessage = $"Error: {expectedText} but got {actualStatusCode}.";

        if (actualStatusCode == 500)
        {
            var parts = new List<string> { baseMessage };
            var apiResponse = ExtractApiResponseMessage(rawBody);
            if (!string.IsNullOrWhiteSpace(apiResponse))
                parts.Add($"Detail: {apiResponse}");

            if (!string.IsNullOrWhiteSpace(traceId))
                parts.Add($"Trace ID: {traceId}");

            return string.Join(Environment.NewLine, parts);
        }

        if (includeBodyForNon500 && !string.IsNullOrWhiteSpace(rawBody))
            return $"{baseMessage} Body: {Truncate(rawBody)}";

        return baseMessage;
    }

    private static string? ExtractApiResponseMessage(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return null;

        var trimmed = Truncate(rawBody).Trim();

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return trimmed;

            string? TryGet(string propertyName)
                => doc.RootElement.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString()
                    : null;

            var message = TryGet("error")
                ?? TryGet("message")
                ?? TryGet("title")
                ?? TryGet("detail");

            return string.IsNullOrWhiteSpace(message) ? trimmed : message.Trim();
        }
        catch
        {
            return trimmed;
        }
    }
}
