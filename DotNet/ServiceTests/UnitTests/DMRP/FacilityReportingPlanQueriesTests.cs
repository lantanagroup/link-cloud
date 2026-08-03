using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Data.Repository;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    [Trait("Category", "UnitTests")]
    public class FacilityReportingPlanQueriesTests
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
            var entity = new FacilityReportingPlan();
            context.FacilityReportingPlans.Add(entity);
            await context.SaveChangesAsync();

            var queries = new FacilityReportingPlanQueries(context);

            var result = await queries.GetAsync(entity.Id);

            Assert.NotNull(result);
            Assert.Equal(entity.Id, result!.Id);
        }

        [Fact]
        public async Task GetAsync_MissingId_ReturnsNull()
        {
            using var context = CreateContext();
            var queries = new FacilityReportingPlanQueries(context);

            var result = await queries.GetAsync(Guid.NewGuid().ToString());

            Assert.Null(result);
        }

        [Fact]
        public async Task PagedSearchAsync_ReturnsPagedRecordsAndMetadata()
        {
            using var context = CreateContext();
            context.FacilityReportingPlans.AddRange(new FacilityReportingPlan(), new FacilityReportingPlan(), new FacilityReportingPlan());
            await context.SaveChangesAsync();

            var queries = new FacilityReportingPlanQueries(context);

            var result = await queries.PagedSearchAsync(pageSize: 2, pageNumber: 1);

            Assert.Equal(2, result.Records.Count);
            Assert.Equal(3, result.Metadata.TotalCount);
        }
    }
}
