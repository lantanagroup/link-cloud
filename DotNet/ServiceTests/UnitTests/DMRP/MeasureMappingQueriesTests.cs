using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    [Trait("Category", "UnitTests")]
    public class MeasureMappingQueriesTests
    {
        private static DmrpDbContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<DmrpDbContext>();
            builder.UseInMemoryDatabase(Guid.NewGuid().ToString());
            return new DmrpDbContext(builder.Options);
        }

        [Fact]
        public async Task GetAsync_ExistingId_ReturnsModel()
        {
            using var context = CreateContext();
            var entity = new MeasureMapping();
            context.MeasureMappings.Add(entity);
            await context.SaveChangesAsync();

            var queries = new MeasureMappingQueries(context);

            var result = await queries.GetAsync(entity.Id);

            Assert.NotNull(result);
            Assert.Equal(entity.Id, result!.Id);
        }

        [Fact]
        public async Task GetAsync_MissingId_ReturnsNull()
        {
            using var context = CreateContext();
            var queries = new MeasureMappingQueries(context);

            var result = await queries.GetAsync(Guid.NewGuid().ToString());

            Assert.Null(result);
        }

        [Fact]
        public async Task PagedSearchAsync_ReturnsPagedRecordsAndMetadata()
        {
            using var context = CreateContext();
            context.MeasureMappings.AddRange(new MeasureMapping(), new MeasureMapping(), new MeasureMapping());
            await context.SaveChangesAsync();

            var queries = new MeasureMappingQueries(context);

            var result = await queries.PagedSearchAsync(pageSize: 2, pageNumber: 1);

            Assert.Equal(2, result.Records.Count);
            Assert.Equal(3, result.Metadata.TotalCount);
        }
    }
}
