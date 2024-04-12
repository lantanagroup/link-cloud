using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Repositories;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Report.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly MongoDbRepository<MeasureReportConfigModel> _datastore;

        public DatabaseHealthCheck(MongoDbRepository<MeasureReportConfigModel> datastore)
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
