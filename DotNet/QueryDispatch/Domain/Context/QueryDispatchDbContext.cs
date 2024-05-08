using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace QueryDispatch.Domain.Context;

public class QueryDispatchDbContext : DbContext
{
    public QueryDispatchDbContext(DbContextOptions<QueryDispatchDbContext> options) : base(options)
    {
    }

    public DbSet<QueryDispatchConfigurationEntity> QueryDispatchConfigurations { get; set; }
    public DbSet<ScheduledReportEntity> ScheduledReports { get; set; }
    public DbSet<ReportPeriodEntity> ReportPeriods { get; set; }
}
