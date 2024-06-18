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
