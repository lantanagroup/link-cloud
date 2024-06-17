using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Filters;

public class YarpConfigFilter : IProxyConfigFilter
{
    private readonly ServiceRegistry _serviceRegistry;

    public YarpConfigFilter(IOptions<ServiceRegistry> serviceRegistry)
    {
        _serviceRegistry = serviceRegistry.Value ?? throw new ArgumentNullException(nameof(serviceRegistry));
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig origCluster, CancellationToken cancel)
    {
        var newDests = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

        string endpoint = origCluster.ClusterId switch
        {
            "AccountService" => _serviceRegistry.AccountServiceUrl,
            "AuditService" => _serviceRegistry.AuditServiceUrl,
            "CensusService" => _serviceRegistry.CensusServiceUrl,
            "DataAcquisitionService" => _serviceRegistry.DataAcquisitionServiceUrl,
            "MeasureEvaluationService" => _serviceRegistry.MeasureServiceUrl,
            "NormalizationService" => _serviceRegistry.NormalizationServiceUrl,
            "NotificationService" => _serviceRegistry.NotificationServiceUrl,
            "ReportService" => _serviceRegistry.ReportServiceUrl,
            "SubmissionService" => _serviceRegistry.SubmissionServiceUrl,
            "TenantService" => _serviceRegistry.SubmissionServiceUrl,
            _ => throw new InvalidOperationException($"Unknown cluster id: {origCluster.ClusterId}")
        };
        var existingDestination = origCluster.Destinations["destination1"];
        var modifiedDest = existingDestination with { Address = endpoint };
        newDests.Add("destination1", modifiedDest);

        return new ValueTask<ClusterConfig>(origCluster with { Destinations = newDests });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        return ValueTask.FromResult(route);
    }
}
