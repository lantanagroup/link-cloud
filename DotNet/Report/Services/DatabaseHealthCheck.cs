using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Report.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IEntityRepository<ReportSchedule> _datastore;

        public DatabaseHealthCheck(IEntityRepository<ReportSchedule> datastore)
        {
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _datastore.HealthCheck(ReportConstants.MeasureReportLoggingIds.HealthCheck, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database connection failed - see inner exception", ex);
            }
        }
    }
}