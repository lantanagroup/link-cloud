using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.TestSuites;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ApiHealthErrorFormattingTests
{
    private const string ExpectedDetail = "An error occurred in our API. Please use the trace id when requesting assistance.";
    private const string ExpectedTraceId = "00-34ce0d8cab778a6ea265c37558c01715-2856c448abd2676b-00";

    private readonly TestSuite _suite = new();

    [Fact]
    public async Task Untyped_step_500_formats_error_message_exactly_for_dashboard_row()
    {
        var result = await _suite.RunUntypedAsync(200, () => Task.FromResult(new LinkApiResponse
        {
            StatusCode = 500,
            RawBody = $$"""
                        {
                          "detail": "{{ExpectedDetail}}"
                        }
                        """,
            TraceId = ExpectedTraceId
        }));

        result.ErrorMessage.Should().Be(string.Join(Environment.NewLine,
            "Error: Expected HTTP 200 but got 500.",
            $"Detail: {ExpectedDetail}",
            $"Trace ID: {ExpectedTraceId}"));
        result.ErrorMessage.Should().NotContain("Body:");
    }

    [Fact]
    public async Task Typed_step_500_formats_error_message_exactly_for_dashboard_row()
    {
        var result = await _suite.RunTypedAsync(200, () => Task.FromResult(new LinkApiResponse<DummyBody>
        {
            StatusCode = 500,
            RawBody = $$"""
                        {
                          "detail": "{{ExpectedDetail}}"
                        }
                        """,
            TraceId = ExpectedTraceId
        }));

        result.ErrorMessage.Should().Be(string.Join(Environment.NewLine,
            "Error: Expected HTTP 200 but got 500.",
            $"Detail: {ExpectedDetail}",
            $"Trace ID: {ExpectedTraceId}"));
        result.ErrorMessage.Should().NotContain("Body:");
    }

    [Fact]
    public async Task Multi_status_step_500_formats_error_message_exactly_for_dashboard_row()
    {
        var result = await _suite.RunAnyOfAsync([200, 204], () => Task.FromResult(new LinkApiResponse
        {
            StatusCode = 500,
            RawBody = $$"""
                        {
                          "detail": "{{ExpectedDetail}}"
                        }
                        """,
            TraceId = ExpectedTraceId
        }));

        result.ErrorMessage.Should().Be(string.Join(Environment.NewLine,
            "Error: Expected one of [200, 204] but got 500.",
            $"Detail: {ExpectedDetail}",
            $"Trace ID: {ExpectedTraceId}"));
        result.ErrorMessage.Should().NotContain("Body:");
    }

    [Fact]
    public async Task Untyped_step_500_with_missing_detail_keeps_error_and_trace_on_separate_lines()
    {
        var result = await _suite.RunUntypedAsync(200, () => Task.FromResult(new LinkApiResponse
        {
            StatusCode = 500,
            RawBody = null,
            TraceId = ExpectedTraceId
        }));

        result.ErrorMessage.Should().Be(string.Join(Environment.NewLine,
            "Error: Expected HTTP 200 but got 500.",
            $"Trace ID: {ExpectedTraceId}"));
    }

    [Fact]
    public async Task Untyped_step_500_with_missing_trace_keeps_error_and_detail_on_separate_lines()
    {
        var result = await _suite.RunUntypedAsync(200, () => Task.FromResult(new LinkApiResponse
        {
            StatusCode = 500,
            RawBody = $$"""
                        {
                          "detail": "{{ExpectedDetail}}"
                        }
                        """,
            TraceId = null
        }));

        result.ErrorMessage.Should().Be(string.Join(Environment.NewLine,
            "Error: Expected HTTP 200 but got 500.",
            $"Detail: {ExpectedDetail}"));
    }

    [Fact]
    public async Task Untyped_step_500_with_missing_detail_and_trace_keeps_single_error_line_only()
    {
        var result = await _suite.RunUntypedAsync(200, () => Task.FromResult(new LinkApiResponse
        {
            StatusCode = 500,
            RawBody = null,
            TraceId = null
        }));

        result.ErrorMessage.Should().Be("Error: Expected HTTP 200 but got 500.");
    }

    [Fact]
    public async Task Untyped_step_preserves_full_request_and_response_bodies()
    {
        var requestBody = new string('r', 1000);
        var responseBody = new string('s', 1000);

        var result = await _suite.RunUntypedAsync(200, () => Task.FromResult(new LinkApiResponse
        {
            StatusCode = 200,
            RequestBody = requestBody,
            RawBody = responseBody
        }));

        result.RequestBody.Should().Be(requestBody);
        result.ResponseBody.Should().Be(responseBody);
        result.RequestBody.Should().HaveLength(1000);
        result.ResponseBody.Should().HaveLength(1000);
    }

    [Fact]
    public async Task Typed_step_preserves_full_request_and_response_bodies()
    {
        var requestBody = new string('r', 1000);
        var responseBody = new string('s', 1000);

        var result = await _suite.RunTypedAsync(200, () => Task.FromResult(new LinkApiResponse<DummyBody>
        {
            StatusCode = 200,
            RequestBody = requestBody,
            RawBody = responseBody
        }));

        result.RequestBody.Should().Be(requestBody);
        result.ResponseBody.Should().Be(responseBody);
        result.RequestBody.Should().HaveLength(1000);
        result.ResponseBody.Should().HaveLength(1000);
    }

    private sealed class TestSuite : ServiceTestSuiteBase
    {
        public override string ServiceName => "TestService";

        public override IReadOnlyList<ApiEndpointDefinition> GetEndpointDefinitions() => [];

        public override Task<IReadOnlyList<ApiTestRunResult>> ExecuteAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApiTestRunResult>>([]);

        public Task<ApiTestRunResult> RunUntypedAsync(int expectedStatusCode, Func<Task<LinkApiResponse>> action) =>
            RunStepAsync("Step", expectedStatusCode, action);

        public Task<ApiTestRunResult> RunTypedAsync(int expectedStatusCode, Func<Task<LinkApiResponse<DummyBody>>> action) =>
            RunStepAsync("Step", expectedStatusCode, action);

        public Task<ApiTestRunResult> RunAnyOfAsync(int[] acceptedStatusCodes, Func<Task<LinkApiResponse>> action) =>
            RunStepAsync("Step", acceptedStatusCodes, action);
    }

    private sealed class DummyBody;
}
