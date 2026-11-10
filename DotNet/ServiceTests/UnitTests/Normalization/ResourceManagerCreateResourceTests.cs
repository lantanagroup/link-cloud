using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ResourceTypeEntity = LantanaGroup.Link.Normalization.Domain.Entities.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class ResourceManagerCreateResourceTests
{
    [Fact]
    public async Task CreateResource_DuplicateInsert_DetachesLoserAndDoesNotInsertAgain()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction("getutcdate", () => DateTime.UtcNow);
        connection.CreateFunction("newid", () => Guid.NewGuid());

        var options = new DbContextOptionsBuilder<NormalizationDbContext>().UseSqlite(connection).Options;
        await using var context = new NormalizationDbContext(options);
        context.Database.EnsureCreated();

        var seededId = Guid.NewGuid();
        context.ResourceTypes.Add(new ResourceTypeEntity { Id = seededId, Name = "Patient" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reads = 0;
        var queries = new Mock<IResourceQueries>();
        queries.Setup(query => query.Get("Patient", It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken _) =>
            {
                reads++;
                ResourceModel? found = reads == 1
                    ? null
                    : new ResourceModel { ResourceTypeId = seededId, ResourceName = "Patient" };
                return Task.FromResult(found);
            });

        var database = new Database(
            context,
            new OperationRepository(context),
            new OperationSequenceRepository(context),
            new ResourceTypeRepository(context),
            new OperationResourceTypeRepository(context),
            new VendorVersionOperationPresetRepository(context));
        var manager = new ResourceManager(database, queries.Object, Mock.Of<IOperationSequenceQueries>(), NullLogger<ResourceManager>.Instance);

        var result = await manager.CreateResource("Patient");

        Assert.Equal(seededId, result.ResourceTypeId);
        Assert.Equal(2, reads);
        Assert.DoesNotContain(
            context.ChangeTracker.Entries<ResourceTypeEntity>(),
            entry => entry.State == EntityState.Added);

        await context.SaveChangesAsync();

        Assert.Equal(1, await context.ResourceTypes.CountAsync(resource => resource.Name == "Patient"));
    }
}
