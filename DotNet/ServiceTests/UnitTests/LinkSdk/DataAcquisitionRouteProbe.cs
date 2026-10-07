using System.Text;
using LantanaGroup.Link.DataAcquisition.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

/// <summary>
/// An in-process TestServer with every Data Acquisition controller mapped, which reports the action a request
/// routes to and answers 204 without running it.
/// </summary>
/// <remarks>
/// No socket is opened. The /api/data alias is deliberately not installed: a request still on the old prefix
/// must not match.
/// </remarks>
public sealed class DataAcquisitionRouteProbe : IAsyncLifetime
{
    private const string MatchedActionHeader = "X-Matched-Action";

    private IHost? _host;
    private HttpClient? _client;

    /// <summary>
    /// Starts the TestServer.
    /// </summary>
    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services => services
                    .AddControllers()
                    .AddApplicationPart(typeof(QueryPlanConfigController).Assembly));
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.Use((HttpContext context, RequestDelegate _) =>
                    {
                        context.Response.Headers[MatchedActionHeader] = DescribeMatchedAction(context) ?? string.Empty;
                        context.Response.StatusCode = StatusCodes.Status204NoContent;
                        return Task.CompletedTask;
                    });
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    /// <summary>
    /// Routes a request and returns the matched action as <c>Controller.Action</c>, or null when nothing matched.
    /// </summary>
    internal async Task<string?> MatchAsync(HttpMethod method, string pathAndQuery, string? body)
    {
        using var request = new HttpRequestMessage(method, pathAndQuery);
        if (!string.IsNullOrEmpty(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await _client!.SendAsync(request);
        var matched = response.Headers.TryGetValues(MatchedActionHeader, out var values)
            ? values.FirstOrDefault()
            : null;

        return string.IsNullOrEmpty(matched) ? null : matched;
    }

    /// <summary>
    /// Stops the TestServer.
    /// </summary>
    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is null)
        {
            return;
        }

        await _host.StopAsync();
        _host.Dispose();
    }

    private static string? DescribeMatchedAction(HttpContext context)
    {
        // A wrong HTTP method routes to a 405 rejection endpoint, which carries no action descriptor.
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is null || action.ControllerTypeInfo.Assembly != typeof(QueryPlanConfigController).Assembly)
        {
            return null;
        }

        return $"{action.ControllerName}.{action.ActionName}";
    }
}
