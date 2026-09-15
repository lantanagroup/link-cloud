using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnitTests.LinkSdk;

internal sealed class CapturedRequest
{
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

/// <summary>
/// Loopback server that accepts exactly one request, captures it, and replies with a
/// canned body. Used to assert the URL, verb and payload an SDK client produces.
/// </summary>
internal sealed class OneShotServer : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpListener _listener;
    private readonly Task<CapturedRequest> _requestTask;

    public string BaseUrl { get; }

    public OneShotServer(string responseBody, int statusCode = 200)
    {
        _listener = StartListener(out var baseUrl);
        BaseUrl = baseUrl;

        _requestTask = System.Threading.Tasks.Task.Run(async () =>
        {
            var context = await _listener.GetContextAsync();
            using var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding ?? Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            var captured = new CapturedRequest
            {
                Method = context.Request.HttpMethod,
                Path = context.Request.Url?.AbsolutePath ?? string.Empty,
                Query = context.Request.Url?.Query ?? string.Empty,
                Body = body
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(responseBody));
            context.Response.Close();

            return captured;
        });
    }

    /// <summary>
    /// Waits for the single expected request, throwing <see cref="TimeoutException"/> rather
    /// than blocking the whole test run when the client never reaches the server.
    /// </summary>
    public Task<CapturedRequest> WaitForRequestAsync(TimeSpan? timeout = null) =>
        _requestTask.WaitAsync(timeout ?? DefaultTimeout);

    public void Dispose()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();

        // Stopping the listener faults the accept loop; observe it so it isn't unhandled.
        _ = _requestTask.ContinueWith(
            static task => _ = task.Exception,
            TaskContinuationOptions.OnlyOnFaulted);
    }

    // The probe must release the port before HttpListener can claim it, so the race is
    // unavoidable — retry a stolen port instead of failing the run.
    private static HttpListener StartListener(out string baseUrl)
    {
        for (var attempt = 1; ; attempt++)
        {
            var url = $"http://127.0.0.1:{GetFreePort()}";
            var listener = new HttpListener();
            listener.Prefixes.Add($"{url}/");

            try
            {
                listener.Start();
                baseUrl = url;
                return listener;
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
            }
        }
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}