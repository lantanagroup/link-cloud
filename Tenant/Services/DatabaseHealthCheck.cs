﻿using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Tenant.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IPersistenceRepository<FacilityConfigModel> _datastore;

        public DatabaseHealthCheck(IPersistenceRepository<FacilityConfigModel> datastore)
        {
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                bool outcome = await _datastore.HealthCheck();

                if (outcome)
                {
                    return HealthCheckResult.Healthy();
                }
                else
                {
                    return HealthCheckResult.Unhealthy();
                }

            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
