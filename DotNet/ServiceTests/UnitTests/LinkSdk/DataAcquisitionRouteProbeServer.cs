using System.Collections.Concurrent;
using System.Net;
using LantanaGroup.Link.DataAcquisition.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

/// <summary>
/// A loopback server with every Data Acquisition controller mapped, which records the action each request
/// routes to and answers 204 without running it.
/// </summary>
/// <remarks>
/// The SDK opens its own connection, so this has to be a real Kestrel listener rather than a TestServer.
/// The /api/data alias is deliberately not installed: an SDK call still on the old prefix must not match.
/// </remarks>
public sealed class DataAcquisitionRouteProbeServer : IAsyncLifetime
{
    private readonly ConcurrentQueue<ProbedRequest> _requests = new();
    private WebApplication? _app;

    /// <summary>
    /// The server's base address, e.g. http://127.0.0.1:51234.
    /// </summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Starts the listener on a free loopback port.
    /// </summary>
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = AppContext.BaseDirectory
        });

        // The test output holds other services' appsettings.json; none of their settings belong here.
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(QueryPlanConfigController).Assembly);

        _app = builder.Build();
        _app.UseRouting();
        _app.Use((HttpContext context, RequestDelegate _) =>
        {
            _requests.Enqueue(new ProbedRequest(context.Request.Method,
                                                context.Request.Path.Value ?? string.Empty,
                                                DescribeMatchedAction(context)));
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        _app.MapControllers();

        await _app.StartAsync();
        BaseUrl = _app.Urls.First();
    }

    /// <summary>
    /// Returns the requests seen since the last call, and forgets them.
    /// </summary>
    internal List<ProbedRequest> TakeRequests()
    {
        var taken = new List<ProbedRequest>();
        while (_requests.TryDequeue(out var request))
        {
            taken.Add(request);
        }

        return taken;
    }

    /// <summary>
    /// Stops the listener.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
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
