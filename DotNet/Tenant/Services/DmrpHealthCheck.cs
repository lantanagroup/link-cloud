using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Config;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Tenant.Services
{
    public sealed class DmrpHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<DmrpSettings> _settings;

        public DmrpHealthCheck(IHttpClientFactory httpClientFactory, IOptions<DmrpSettings> settings)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var settings = _settings.Value;
            if (!settings.Enabled)
            {
                return HealthCheckResult.Healthy("DMRP is disabled.");
            }

            if (!Uri.TryCreate(settings.Api.BaseUrl?.TrimEnd('/') + "/msc?nhsnorgid=0", UriKind.Absolute,
                    out var healthUri) || (healthUri.Scheme != Uri.UriSchemeHttp && healthUri.Scheme != Uri.UriSchemeHttps))
            {
                return HealthCheckResult.Unhealthy("DMRP API base URL is missing or invalid.");
            }

            try
            {
                using var client = _httpClientFactory.CreateClient(DmrpApiClient.HttpClientName);
                using var response = await client.GetAsync(healthUri, cancellationToken);
                return response.IsSuccessStatusCode || 
                        response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                        response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                        response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy($"DMRP health endpoint answered {(int)response.StatusCode}.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return HealthCheckResult.Unhealthy("DMRP health endpoint could not be reached.");
            }
        }
    }
}