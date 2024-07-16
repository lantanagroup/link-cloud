using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Filters;

public class YarpConfigFilter : IProxyConfigFilter
{
    private readonly ILogger<YarpConfigFilter> _logger;
    private readonly ServiceRegistry _serviceRegistry;

    public YarpConfigFilter(ILogger<YarpConfigFilter> logger, IOptions<ServiceRegistry> serviceRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceRegistry = serviceRegistry.Value ?? throw new ArgumentNullException(nameof(serviceRegistry));
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig origCluster, CancellationToken cancel)
    {
        var newDests = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);


        string endpoint = origCluster.ClusterId switch
        {
            "AccountService" => _serviceRegistry.AccountServiceUrl ?? string.Empty,
            "AuditService" => _serviceRegistry.AuditServiceUrl ?? string.Empty,
            "CensusService" => _serviceRegistry.CensusServiceUrl ?? string.Empty,
            "DataAcquisitionService" => _serviceRegistry.DataAcquisitionServiceUrl ?? string.Empty,
            "MeasureEvaluationService" => _serviceRegistry.MeasureServiceUrl ?? string.Empty,
            "NormalizationService" => _serviceRegistry.NormalizationServiceUrl ?? string.Empty,
            "NotificationService" => _serviceRegistry.NotificationServiceUrl ?? string.Empty,
            "QueryDispatchService" => _serviceRegistry.QueryDispatchServiceUrl ?? string.Empty,
            "ReportService" => _serviceRegistry.ReportServiceUrl ?? string.Empty,
            "SubmissionService" => _serviceRegistry.SubmissionServiceUrl ?? string.Empty,
            "TenantService" => _serviceRegistry.TenantService.TenantServiceUrl ?? string.Empty,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning("No endpoint found in registry for cluster {cluser}", origCluster.ClusterId);
            return ValueTask.FromResult(origCluster);
        }

        var existingDestination = origCluster.Destinations?["destination1"];
        var modifiedDest = existingDestination with { Address = endpoint };
        newDests.Add("destination1", modifiedDest);

        return new ValueTask<ClusterConfig>(origCluster with { Destinations = newDests });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        return ValueTask.FromResult(route);
    }
}
