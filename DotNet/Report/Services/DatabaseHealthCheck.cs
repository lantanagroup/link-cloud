using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Report.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IBaseEntityRepository<ReportScheduleModel> _datastore;

        public DatabaseHealthCheck(IBaseEntityRepository<ReportScheduleModel> datastore)
        {
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return await _datastore.HealthCheck(ReportConstants.MeasureReportLoggingIds.HealthCheck);
        }
    }

}
