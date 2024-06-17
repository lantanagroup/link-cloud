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
            "AccountService" => _serviceRegistry.AccountServiceApiUrl,
            "AuditService" => _serviceRegistry.AuditServiceApiUrl,
            "CensusService" => _serviceRegistry.CensusServiceApiUrl,
            "DataAcquisitionService" => _serviceRegistry.DataAcquisitionServiceApiUrl,
            "MeasureEvaluationService" => _serviceRegistry.MeasureServiceApiUrl,
            "NormalizationService" => _serviceRegistry.NormalizationServiceApiUrl,
            "NotificationService" => _serviceRegistry.NotificationServiceApiUrl,
            "ReportService" => _serviceRegistry.ReportServiceApiUrl,
            "SubmissionService" => _serviceRegistry.SubmissionServiceApiUrl,
            "TenantService" => _serviceRegistry.SubmissionServiceApiUrl,
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
