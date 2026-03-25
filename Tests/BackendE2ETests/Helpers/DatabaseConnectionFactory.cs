using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Report.Data;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Provides connection strings and EF DbContext instances for direct database
/// validation during E2E tests. Connection parameters are derived from the
/// docker-compose .env configuration, with environment variable overrides for CI.
/// </summary>
public static class DatabaseConnectionFactory
{
    private static readonly string Server = Environment.GetEnvironmentVariable("E2E_SQL_SERVER") ?? "localhost,1433";
    private static readonly string Password = Environment.GetEnvironmentVariable("E2E_SQL_PASSWORD") ?? "7h3I^xMY%cgO";
    private static readonly string UserId = Environment.GetEnvironmentVariable("E2E_SQL_USER") ?? "sa";

    public static string GetConnectionString(string database)
    {
        return $"Server=tcp:{Server};Initial Catalog={database};User ID={UserId};Password={Password};" +
               "TrustServerCertificate=True;Encrypt=True;Connection Timeout=30;";
    }

    /// <summary>
    /// Creates a <see cref="ReportDbContext"/> connected to the live Docker SQL Server.
    /// The context is configured as no-tracking for read-only validation queries.
    /// </summary>
    public static ReportDbContext CreateReportDbContext()
    {
        var connectionString = GetConnectionString(Databases.Report);
        var options = new DbContextOptionsBuilder<ReportDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new ReportDbContext(options);
    }

    /// <summary>
    /// Creates a <see cref="DataAcquisitionDbContext"/> connected to the live Docker SQL Server.
    /// The context is configured as no-tracking for read-only validation queries.
    /// </summary>
    public static DataAcquisitionDbContext CreateDataAcquisitionDbContext()
    {
        var connectionString = GetConnectionString(Databases.DataAcquisition);
        var options = new DbContextOptionsBuilder<DataAcquisitionDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new DataAcquisitionDbContext(options);
    }

    /// <summary>
    /// Creates a <see cref="NormalizationDbContext"/> connected to the live Docker SQL Server.
    /// The context is configured as no-tracking for read-only validation queries.
    /// </summary>
    public static NormalizationDbContext CreateNormalizationDbContext()
    {
        var connectionString = GetConnectionString(Databases.Normalization);
        var options = new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseSqlServer(connectionString)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new NormalizationDbContext(options);
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
