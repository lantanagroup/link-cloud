﻿using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.Report.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IEntityRepository<MeasureReportConfigModel> _datastore;

        public DatabaseHealthCheck(IEntityRepository<MeasureReportConfigModel> datastore)
        {
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return await _datastore.HealthCheck(ReportConstants.MeasureReportLoggingIds.HealthCheck);
        }
    }

}
