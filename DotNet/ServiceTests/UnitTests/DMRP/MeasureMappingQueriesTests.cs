using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Models;
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
            context.MeasureMappings.AddRange(
                new MeasureMapping { Measure = "CMS130v13", DQM = "Preventive Care" },
                new MeasureMapping { Measure = "CMS122v12", DQM = "Diabetes Care" },
                new MeasureMapping { Measure = "CMS2v13", DQM = "Immunization Status" });
            await context.SaveChangesAsync();

            var queries = CreateQueries(context);

            var result = await queries.PagedSearchAsync(new SearchMeasureMappingDto
            {
                PageSize = 2,
                PageNumber = 1
            });

            Assert.Equal(2, result.Records.Count);
            Assert.Equal(3, result.Metadata.TotalCount);
        }

        [Fact]
        public async Task PagedSearchAsync_ProvidedFields_ReturnsOnlyMatchingRecords()
        {
            using var context = CreateContext();
            var matchingMapping = new MeasureMapping
            {
                Measure = "CMS130v13",
                DQM = "Preventive Care",
                Frequency = Frequency.Monthly
            };
            context.MeasureMappings.AddRange(
                matchingMapping,
                new MeasureMapping
                {
                    Measure = "CMS130v13",
                    DQM = "Diabetes Care",
                    Frequency = Frequency.Daily
                },
                new MeasureMapping
                {
                    Measure = "CMS122v12",
                    DQM = "Preventive Care",
                    Frequency = Frequency.Monthly
                },
                new MeasureMapping
                {
                    Measure = "CMS130v13",
                    DQM = "Immunization Status",
                    Frequency = Frequency.Monthly
                });
            await context.SaveChangesAsync();

            var queries = CreateQueries(context);

            var result = await queries.PagedSearchAsync(new SearchMeasureMappingDto
            {
                Measure = matchingMapping.Measure,
                DQM = matchingMapping.DQM,
                Frequency = matchingMapping.Frequency
            });

            var record = Assert.Single(result.Records);
            Assert.Equal(matchingMapping.Id, record.Id);
            Assert.Equal(1, result.Metadata.TotalCount);
        }

        [Theory]
        [InlineData("cms130", null)]       // lower-case measure
        [InlineData("cMs130V", null)]      // mixed-case measure
        [InlineData(null, "preventive")]   // lower-case DQM
        [InlineData(null, "pReVeNtIvE")]   // mixed-case DQM
        [InlineData("  cms130  ", null)]   // padded measure — filters trim before matching
        [InlineData(null, " preventive ")] // padded DQM
        public async Task PagedSearchAsync_MeasureAndDqm_MatchCaseInsensitively(string? measure, string? dqm)
        {
            using var context = CreateContext();
            context.MeasureMappings.AddRange(
                new MeasureMapping { Measure = "CMS130v13", DQM = "Preventive Care" },
                new MeasureMapping { Measure = "CMS122v12", DQM = "Diabetes Care" });
            await context.SaveChangesAsync();

            var queries = CreateQueries(context);

            // SQLite's LIKE happens to be ASCII-case-insensitive, so these pass here even
            // without explicit lower-casing — they pin the contract. The case that motivates
            // the explicit ToLower in the query is a case-sensitive SQL Server collation,
            // which this harness cannot reproduce.
            var result = await queries.PagedSearchAsync(new SearchMeasureMappingDto
            {
                Measure = measure,
                DQM = dqm
            });

            var record = Assert.Single(result.Records);
            Assert.Equal("CMS130v13", record.Measure);
        }

        [Fact]
        public async Task PagedSearchAsync_PartialMeasureAndDqm_MatchAsSubstrings()
        {
            using var context = CreateContext();
            context.MeasureMappings.AddRange(
                new MeasureMapping { Measure = "CMS130v13", DQM = "Preventive Care" },
                new MeasureMapping { Measure = "CMS122v12", DQM = "Diabetes Care" },
                new MeasureMapping { Measure = "ACH", DQM = "Immunization Status" });
            await context.SaveChangesAsync();

            var queries = CreateQueries(context);

            // The Admin UI searches on every keystroke, so a prefix has to narrow the list.
            var byMeasure = await queries.PagedSearchAsync(new SearchMeasureMappingDto { Measure = "CMS1" });
            Assert.Equal(2, byMeasure.Records.Count);

            // Substring anywhere, not only a prefix.
            var byDqm = await queries.PagedSearchAsync(new SearchMeasureMappingDto { DQM = "Care" });
            Assert.Equal(2, byDqm.Records.Count);

            var noMatch = await queries.PagedSearchAsync(new SearchMeasureMappingDto { Measure = "CMS130v13X" });
            Assert.Empty(noMatch.Records);
        }

        [Fact]
        public async Task PagedSearchAsync_BothFiltersCased_CombineCaseInsensitively()
        {
            using var context = CreateContext();
            context.MeasureMappings.AddRange(
                new MeasureMapping { Measure = "CMS130v13", DQM = "Preventive Care" },
                new MeasureMapping { Measure = "ACH", DQM = "Immunization Status" });
            await context.SaveChangesAsync();

            var queries = CreateQueries(context);

            var result = await queries.PagedSearchAsync(new SearchMeasureMappingDto { Measure = "aCh", DQM = "immunization" });

            var record = Assert.Single(result.Records);
            Assert.Equal("ACH", record.Measure);
        }
    }
}
