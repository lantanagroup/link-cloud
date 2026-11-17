using LantanaGroup.Link.Account.Persistence;
using LantanaGroup.Link.Audit.Persistance;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.MockDmrpApi.Domain.Context;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using QueryDispatch.Domain.Context;
using Submission.Data;

namespace ServiceTests.TestHelpers;

/// <summary>
/// Every DbContext that a service migrates at startup through AutoMigrateEF, keyed by service name.
/// </summary>
public static class AutoMigratedContexts
{
    private static readonly IReadOnlyDictionary<string, Func<string, DbContext>> Factories =
        new Dictionary<string, Func<string, DbContext>>
        {
            ["Account"] = connectionString => new AccountDbContext(Options<AccountDbContext>(connectionString)),
            ["Audit"] = connectionString => new AuditDbContext(Options<AuditDbContext>(connectionString)),
            ["Census"] = connectionString => new CensusContext(Options<CensusContext>(connectionString)),
            ["DataAcquisition"] = connectionString =>
                new DataAcquisitionDbContext(Options<DataAcquisitionDbContext>(connectionString)),
            ["MockDmrpApi"] = connectionString =>
                new ReportingPlanDbContext(Options<ReportingPlanDbContext>(connectionString)),
            ["NhsnApp"] = connectionString => new NhsnAppDbContext(Options<NhsnAppDbContext>(connectionString)),
            ["Normalization"] = connectionString =>
                new NormalizationDbContext(Options<NormalizationDbContext>(connectionString)),
            ["QueryDispatch"] = connectionString =>
                new QueryDispatchDbContext(Options<QueryDispatchDbContext>(connectionString)),
            ["Report"] = connectionString => new ReportDbContext(Options<ReportDbContext>(connectionString)),
            ["Submission"] = connectionString => new SubmissionContext(Options<SubmissionContext>(connectionString)),
            ["Tenant"] = connectionString => new TenantDbContext(Options<TenantDbContext>(connectionString)),
        };

    /// <summary>
    /// The service names, in a stable order, for xUnit theory data.
    /// </summary>
    public static IEnumerable<string> Names => Factories.Keys;

    /// <summary>
    /// Creates the named service's DbContext on SQL Server, the provider its migrations were scaffolded for.
    /// </summary>
    public static DbContext Create(string name, string connectionString)
    {
        return Factories[name](connectionString);
    }

    private static DbContextOptions<TContext> Options<TContext>(string connectionString) where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString)
            .Options;
    }
}
