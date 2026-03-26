using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Provides connection strings and EF DbContext instances for direct database
/// validation during E2E tests. Connection parameters are sourced from
/// <see cref="AutomationConfig.DatabaseConfig"/>.
/// </summary>
public class DatabaseConnectionFactory
{
    private readonly AutomationConfig.DatabaseConfig _dbConfig;

    public DatabaseConnectionFactory(AutomationConfig.DatabaseConfig dbConfig)
    {
        _dbConfig = dbConfig;
    }

    public string GetConnectionString(string database)
    {
        return $"Server=tcp:{_dbConfig.Server};Initial Catalog={database};User ID={_dbConfig.UserId};Password={_dbConfig.Password};" +
               "TrustServerCertificate=True;Encrypt=True;Connection Timeout=30;";
    }

    /// <summary>
    /// Creates a <see cref="ReportDbContext"/> connected to the target SQL Server.
    /// The context is configured as no-tracking for read-only validation queries.
    /// </summary>
    public ReportDbContext CreateReportDbContext()
    {
        var connectionString = GetConnectionString(Databases.Report);
        var options = new DbContextOptionsBuilder<ReportDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new ReportDbContext(options);
    }

    /// <summary>
    /// Creates a <see cref="DataAcquisitionDbContext"/> connected to the target SQL Server.
    /// The context is configured as no-tracking for read-only validation queries.
    /// </summary>
    public DataAcquisitionDbContext CreateDataAcquisitionDbContext()
    {
        var connectionString = GetConnectionString(Databases.DataAcquisition);
        var options = new DbContextOptionsBuilder<DataAcquisitionDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new DataAcquisitionDbContext(options);
    }

    /// <summary>
    /// Creates a <see cref="NormalizationDbContext"/> connected to the target SQL Server.
    /// The context is configured as no-tracking for read-only validation queries.
    /// </summary>
    public NormalizationDbContext CreateNormalizationDbContext()
    {
        var connectionString = GetConnectionString(Databases.Normalization);
        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new NormalizationDbContext(options);
    }

    /// <summary>
    /// Creates a <see cref="TenantDbContext"/> connected to the target SQL Server.
    /// The context is configured as no-tracking for read-only validation queries.
    /// </summary>
    public TenantDbContext CreateTenantDbContext()
    {
        var connectionString = GetConnectionString(Databases.Tenant);
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new TenantDbContext(options);
    }

    public static class Databases
    {
        public const string Report = "link-report";
        public const string Submission = "link-submission";
        public const string Tenant = "link-tenant";
        public const string DataAcquisition = "link-dataacquisition";
        public const string Normalization = "link-normalization";
        public const string Census = "link-census";
        public const string QueryDispatch = "link-querydispatch";
        public const string Audit = "link-audit";
        public const string Account = "link-account";
        public const string Validation = "link-validation";
    }
}
