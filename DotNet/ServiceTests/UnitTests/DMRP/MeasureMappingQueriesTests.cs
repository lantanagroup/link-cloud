using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    /// <summary>
    /// DMRP entities are persisted through the host service's context, so these exercise the queries
    /// against a <see cref="TenantDbContext"/>.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class MeasureMappingQueriesTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public MeasureMappingQueriesTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        public void Dispose() => _connection.Dispose();

        private TenantDbContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<TenantDbContext>();
            builder.UseSqlite(_connection);
            var context = new TenantDbContext(builder.Options);
            context.Database.EnsureCreated();
            return context;
        }

        private static MeasureMappingQueries CreateQueries(TenantDbContext context) =>
            new(new EntityRepository<MeasureMapping, TenantDbContext>(context));

        [Fact]
        public async Task GetAsync_ExistingId_ReturnsModel()
        {
            using var context = CreateContext();
            var entity = new MeasureMapping();
            context.MeasureMappings.Add(entity);
            await context.SaveChangesAsync();

            var queries = CreateQueries(context);

            var result = await queries.GetAsync(entity.Id);

            Assert.NotNull(result);
            Assert.Equal(entity.Id, result!.Id);
        }

        [Fact]
        public async Task GetAsync_MissingId_ReturnsNull()
        {
            using var context = CreateContext();
            var queries = CreateQueries(context);

            var result = await queries.GetAsync(Guid.NewGuid().ToString());

            Assert.Null(result);
        }

        [Fact]
        public async Task PagedSearchAsync_ReturnsPagedRecordsAndMetadata()
        {
            using var context = CreateContext();
            context.MeasureMappings.AddRange(new MeasureMapping(), new MeasureMapping(), new MeasureMapping());
            await context.SaveChangesAsync();

            var queries = CreateQueries(context);

            var result = await queries.PagedSearchAsync(pageSize: 2, pageNumber: 1);

            Assert.Equal(2, result.Records.Count);
            Assert.Equal(3, result.Metadata.TotalCount);
        }
    }
}
