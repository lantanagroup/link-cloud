using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UnitTests.LinkSdk;

/// <summary>
/// DMRP is a module hosted by the Tenant service rather than a service of its own, so these confirm
/// the client reaches it at the routes its controllers actually expose.
/// </summary>
[Trait("Category", "UnitTests")]
public class DmrpServiceClientTests
{
    private const string EmptyPage = "{\"records\":[],\"metadata\":null}";

    /// <summary>
    /// The measure mapping collection route only accepts POST; the searchable listing is a segment
    /// below it. Reading the collection answers 404, which is indistinguishable from the module being
    /// switched off.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchMeasureMappingsAsync_ReadsTheSearchRoute()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchMeasureMappingsAsync(pageSize: 5, pageNumber: 2);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/dmrp/measure-mappings/search", request.Path);
        Assert.Equal("?pageSize=5&pageNumber=2", request.Query);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchMeasureMappingsAsync_SendsTheFiltersItWasGiven()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchMeasureMappingsAsync(measure: "HOB", dqm: "dqm-monthly",
            frequency: Frequency.Monthly, pageSize: 1, pageNumber: 1);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("/api/dmrp/measure-mappings/search", request.Path);
        Assert.Contains("measure=HOB", request.Query);
        Assert.Contains("dqm=dqm-monthly", request.Query);
        Assert.Contains("frequency=Monthly", request.Query);
    }

    /// <summary>
    /// A filter that was not given is left off the request rather than sent empty, which the API would
    /// read as a filter on the empty string.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchMeasureMappingsAsync_OmitsFiltersThatWereNotGiven()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchMeasureMappingsAsync(measure: "HOB");
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.DoesNotContain("dqm=", request.Query);
        Assert.DoesNotContain("frequency=", request.Query);
    }

    /// <summary>
    /// This route exists whenever the module is registered and does not when it is not, which is what
    /// makes it usable as a probe for whether DMRP is enabled.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task SearchFacilityReportingPlansAsync_ReadsTheReportingPlansCollection()
    {
        using var server = new OneShotServer(EmptyPage);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchFacilityReportingPlansAsync(pageSize: 1, pageNumber: 1);
        var request = await server.WaitForRequestAsync();
        await callTask;

        Assert.Equal("GET", request.Method);
        Assert.Equal("/api/dmrp/reporting-plans", request.Path);
    }

    [Fact]
    public async System.Threading.Tasks.Task SearchMeasureMappingsAsync_ReportsNotFoundRatherThanThrowing()
    {
        using var server = new OneShotServer("{}", statusCode: 404);
        using var client = CreateClient(server.BaseUrl);

        var callTask = client.SearchMeasureMappingsAsync(measure: "HOB");
        await server.WaitForRequestAsync();
        var response = await callTask;

        Assert.Equal(404, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
    }

    private static DmrpServiceClient CreateClient(string baseUrl)
    {
        return new DmrpServiceClient(
            Options.Create(new ServiceRegistry
            {
                TenantService = new TenantServiceRegistration { TenantServiceUrl = baseUrl }
            }),
            Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
            Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
            new Mock<ICreateSystemToken>().Object);
    }

    private sealed class CapturedRequest
    {
        public string Method { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Query { get; init; } = string.Empty;
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

                var captured = new CapturedRequest
                {
                    Method = context.Request.HttpMethod,
                    Path = context.Request.Url?.AbsolutePath ?? string.Empty,
                    Query = context.Request.Url?.Query ?? string.Empty
                };

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                var buffer = Encoding.UTF8.GetBytes(responseBody);
                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.Close();

                return captured;
            });
        }

        public System.Threading.Tasks.Task<CapturedRequest> WaitForRequestAsync() => _requestTask;

        public void Dispose()
        {
            if (_listener.IsListening)
                _listener.Stop();

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
