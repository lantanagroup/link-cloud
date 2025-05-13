using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Census.Application.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IBaseEntityRepository<CensusConfigEntity> _datastore;

        public DatabaseHealthCheck(IBaseEntityRepository<CensusConfigEntity> datastore)
        {
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _datastore.HealthCheck(CensusConstants.CensusLoggingIds.HealthCheck);

            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
