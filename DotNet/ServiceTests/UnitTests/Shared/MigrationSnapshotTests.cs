using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ServiceTests.TestHelpers;

namespace UnitTests.Shared;

/// <summary>
/// Fails when a DbContext's model has changes that no migration records. Every service migrates at startup,
/// and from EF Core 9 Database.Migrate() throws when the model and the latest migration snapshot differ.
/// </summary>
[Trait("Category", "UnitTests")]
public sealed class MigrationSnapshotTests
{
    // No connection is opened. The provider only has to match the one the migrations were scaffolded for.
    private const string UnusedConnectionString = "Server=unused;Database=unused";

    /// <summary>
    /// The name of every DbContext that a service migrates at startup.
    /// </summary>
    public static TheoryData<string> ContextNames => new(AutoMigratedContexts.Names);

    [Theory]
    [MemberData(nameof(ContextNames))]
    public void Model_ComparedWithLatestMigrationSnapshot_HasNoPendingChanges(string contextName)
    {
        using var context = AutoMigratedContexts.Create(contextName, UnusedConnectionString);

        var pendingChanges = GetPendingChanges(context);

        Assert.True(pendingChanges.Count == 0,
                    $"{contextName}'s model has changes that no migration records. " +
                    "Add a migration from the service folder with 'dotnet ef migrations add'. " +
                    $"Pending: {string.Join("; ", pendingChanges.Select(Describe))}");
    }

    // EF Core 8 has no public HasPendingModelChanges(); this is what 'dotnet ef migrations
    // has-pending-model-changes' does. On EF Core 9 or later, use context.Database.HasPendingModelChanges().
    private static IReadOnlyList<MigrationOperation> GetPendingChanges(DbContext context)
    {
        var snapshotModel = context.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;

        if (snapshotModel is IMutableModel mutableModel)
        {
            snapshotModel = mutableModel.FinalizeModel();
        }

        if (snapshotModel is not null)
        {
            snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel);
        }

        var currentModel = context.GetService<IDesignTimeModel>().Model;

        return context.GetService<IMigrationsModelDiffer>().GetDifferences(snapshotModel?.GetRelationalModel(),
                                                                           currentModel.GetRelationalModel());
    }

    private static string Describe(MigrationOperation operation)
    {
        var operationName = operation.GetType().Name;

        return operation switch
        {
            ColumnOperation column => $"{operationName} {Qualify(column.Schema, column.Table)}.{column.Name}",
            ITableMigrationOperation table => $"{operationName} {Qualify(table.Schema, table.Table)}",
            _ => operationName,
        };
    }

    private static string Qualify(string? schema, string table)
    {
        return string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
    }
}
