using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnitTests.LinkSdk;

[Trait("Category", "UnitTests")]
public class DataAcquisitionServiceClientTests
{
    [Fact]
    public async System.Threading.Tasks.Task GetAcquisitionLogNotesAsync_CallsExpectedEndpoint()
    {
        using var server = new OneShotServer("[\"n1\",\"n2\"]");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetAcquisitionLogNotesAsync(42);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/data/acquisition-logs/42/notes", request.Path);
        Assert.Equal(2, result.Body!.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetReportStatisticsAsync_CallsExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.GetReportStatisticsAsync("r1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/data/acquisition-logs/report/r1/statistics", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProcessAcquisitionLogsBulkAsync_PostsIds()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var ids = new List<long> { 11, 22 };
        var callTask = client.ProcessAcquisitionLogsBulkAsync(ids);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/process-bulk", request.Path);
        Assert.Contains("11", request.Body);
        Assert.Contains("22", request.Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProcessAcquisitionLogAsync_CallsExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.ProcessAcquisitionLogAsync(7);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/7/process", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelAcquisitionLogsByFilterAsync_PostsFilterAndQueryString()
    {
        const string response = "{\"requested\":2,\"cancelled\":1,\"ineligible\":1}";
        using var server = new OneShotServer(response);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CancelAcquisitionLogsByFilterAsync(new { facilityId = "f1" }, minAgeHours: 0);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/cancel-by-filter", request.Path);
        Assert.Contains("minAgeHours=0", request.Query);
        Assert.Contains("facilityId", request.Body);
        Assert.NotNull(result.Body);
        Assert.Equal(2, result.Body.Requested);
        Assert.Equal(1, result.Body.Cancelled);
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelAcquisitionLogsBulkAsync_PostsIdsAndQueryString()
    {
        const string response = "{\"requested\":1,\"cancelled\":1,\"ineligible\":0}";
        using var server = new OneShotServer(response);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.CancelAcquisitionLogsBulkAsync([99], minAgeHours: 3);
        var request = await server.WaitForRequestAsync();
        var result = await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/cancel-bulk", request.Path);
        Assert.Contains("minAgeHours=3", request.Query);
        Assert.Contains("99", request.Body);
        Assert.NotNull(result.Body);
        Assert.Equal(1, result.Body.Requested);
        Assert.Equal(1, result.Body.Cancelled);
    }

    [Fact]
    public async System.Threading.Tasks.Task ProcessAcquisitionLogsByFilterAsync_PostsFilterBody()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.ProcessAcquisitionLogsByFilterAsync(new { facilityId = "f2", reportId = "r2" });
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/data/acquisition-logs/process-by-filter", request.Path);
        Assert.Contains("facilityId", request.Body);
        Assert.Contains("reportId", request.Body);
    }

    [Fact]
    public async System.Threading.Tasks.Task RestoreLogsByFacilityAsync_PatchesExpectedEndpoint()
    {
        using var server = new OneShotServer("1");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.RestoreLogsByFacilityAsync("f1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PATCH", request.Method);
        Assert.Equal("/api/data/acquisition-logs/facility/f1/restore", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task RestoreLogsByReportTrackingIdAsync_PatchesExpectedEndpoint()
    {
        using var server = new OneShotServer("1");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.RestoreLogsByReportTrackingIdAsync("rpt-1");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("PATCH", request.Method);
        Assert.Equal("/api/data/acquisition-logs/report/rpt-1/restore", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteAcquisitionLogAsync_DeletesExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.DeleteAcquisitionLogAsync(99);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/data/acquisition-logs/99", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task SoftDeleteLogsByReportTrackingIdAsync_DeletesExpectedEndpoint()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SoftDeleteLogsByReportTrackingIdAsync("rpt-2");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("DELETE", request.Method);
        Assert.Equal("/api/data/acquisition-logs/report/rpt-2", request.Path);
    }

    private static DataAcquisitionServiceClient CreateClient(string baseUrl)
    {
        var serviceRegistry = Options.Create(new ServiceRegistry
        {
            DataAcquisitionServiceUrl = baseUrl
        });

        var bearerOptions = Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions
        {
            AllowAnonymous = true
        });

        var tokenSettings = Options.Create(new LinkTokenServiceSettings
        {
            SigningKey = "test"
        });

        var tokenService = new Mock<ICreateSystemToken>();

        return new DataAcquisitionServiceClient(serviceRegistry, bearerOptions, tokenSettings, tokenService.Object);
    }

    private sealed class CapturedRequest
    {
        public string Method { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Query { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
    }

    private sealed class OneShotServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly System.Threading.Tasks.Task<CapturedRequest> _requestTask;

        public string BaseUrl { get; }

        public OneShotServer(string responseBody, int statusCode = 200)
        {
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";

            _listener = new HttpListener();
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();

            _requestTask = System.Threading.Tasks.Task.Run(async () =>
            {
                var context = await _listener.GetContextAsync();

                string body;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync();
                }

                var captured = new CapturedRequest
                {
                    Method = context.Request.HttpMethod,
                    Path = context.Request.Url?.AbsolutePath ?? string.Empty,
                    Query = context.Request.Url?.Query ?? string.Empty,
                    Body = body
                };

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                var buffer = Encoding.UTF8.GetBytes(responseBody);
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();

                return captured;
            });
        }

        public System.Threading.Tasks.Task<CapturedRequest> WaitForRequestAsync() => _requestTask;

        public void Dispose()
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
            _listener.Close();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
