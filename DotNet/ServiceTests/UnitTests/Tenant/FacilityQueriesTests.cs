using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Models;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant
{
    [Trait("Category", "UnitTests")]
    public class FacilityQueriesTests
    {
        private TenantDbContext _context;
        private Mock<IEntityRepository<Facility>> _mockRepository;
        private FacilityQueries _queries;
        private List<Facility> _sampleFacilities;

        public FacilityQueriesTests()
        {
            var builder = new DbContextOptionsBuilder<TenantDbContext>();
            builder.UseInMemoryDatabase(Guid.NewGuid().ToString());
            var options = builder.Options;

            _context = new TenantDbContext(options);
            _mockRepository = new Mock<IEntityRepository<Facility>>();

            _queries = new FacilityQueries(_context, _mockRepository.Object);

            // Sample data
            _sampleFacilities = new List<Facility>
            {
                new Facility
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "FAC001",
                    FacilityName = "Facility One",
                    TimeZone = "UTC",
                    ScheduledReports = new ScheduledReportModel
                    {
                        Daily = new string[] { "ReportA" },
                        Weekly = new string[] { "ReportB" },
                        Monthly = new string[] { "ReportC" }
                    },
                    CreateDate = DateTime.UtcNow
                },
                new Facility
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "FAC002",
                    FacilityName = "Facility Two",
                    TimeZone = "EST",
                    ScheduledReports = new ScheduledReportModel
                    {
                        Daily = new string[] { "ReportD" },
                        Weekly = new string[] { "ReportE" },
                        Monthly = new string[] { "ReportF" }
                    },
                    CreateDate = DateTime.UtcNow
                },
                new Facility
                {
                    Id = Guid.NewGuid(),
                    FacilityId = "FAC003",
                    FacilityName = "Another Facility One",
                    TimeZone = "PST",
                    ScheduledReports = new ScheduledReportModel
                    {
                        Daily = Array.Empty<string>(),
                        Weekly = Array.Empty<string>(),
                        Monthly = Array.Empty<string>()
                    },
                    CreateDate = DateTime.UtcNow
                }
            };

            _context.Facilities.AddRange(_sampleFacilities);
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetAsync_ById_ReturnsFacility_WhenExists()
        {
            var facility = _sampleFacilities[0];
            var result = await _queries.GetAsync(facility.Id);

            Assert.NotNull(result);
            Assert.Equal(facility.Id, result.Id);
            Assert.Equal(facility.FacilityId, result.FacilityId);
            Assert.Equal(facility.FacilityName, result.FacilityName);
            Assert.Equal(facility.TimeZone, result.TimeZone);
            Assert.Equal(facility.ScheduledReports.Daily, result.ScheduledReports.Daily);
            Assert.Equal(facility.ScheduledReports.Weekly, result.ScheduledReports.Weekly);
            Assert.Equal(facility.ScheduledReports.Monthly, result.ScheduledReports.Monthly);
        }

        [Fact]
        public async Task GetAsync_ById_ReturnsNull_WhenNotExists()
        {
            var result = await _queries.GetAsync(Guid.NewGuid());
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_ByFacilityId_ReturnsFacility_WhenExists()
        {
            var facility = _sampleFacilities[0];
            var result = await _queries.GetAsync(facility.FacilityId);

            Assert.NotNull(result);
            Assert.Equal(facility.FacilityId, result.FacilityId);
        }

        [Fact]
        public async Task GetAsync_ByFacilityIdAndName_ReturnsFacility_WhenExists()
        {
            var facility = _sampleFacilities[0];
            var result = await _queries.GetAsync(facility.FacilityId, facility.FacilityName);

            Assert.NotNull(result);
            Assert.Equal(facility.FacilityId, result.FacilityId);
            Assert.Equal(facility.FacilityName, result.FacilityName);
        }

        [Fact]
        public async Task GetAsync_ByFacilityIdAndName_ReturnsNull_WhenNameMismatch()
        {
            var facility = _sampleFacilities[0];
            var result = await _queries.GetAsync(facility.FacilityId, "Wrong Name");
            Assert.Null(result);
        }

        [Fact]
        public async Task SearchAsync_ThrowsException_WhenNoCriteria()
        {
            var model = new FacilitySearchModel();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _queries.SearchAsync(model));
        }

        [Fact]
        public async Task SearchAsync_ById_ReturnsMatchingFacilities()
        {
            var facility = _sampleFacilities[0];
            var model = new FacilitySearchModel { Id = facility.Id };
            var results = await _queries.SearchAsync(model);

            Assert.Single(results);
            Assert.Equal(facility.Id, results[0].Id);
        }

        [Fact]
        public async Task SearchAsync_ByFacilityId_ReturnsMatchingFacilities()
        {
            var facility = _sampleFacilities[0];
            var model = new FacilitySearchModel { FacilityId = facility.FacilityId };
            var results = await _queries.SearchAsync(model);

            Assert.Single(results);
            Assert.Equal(facility.FacilityId, results[0].FacilityId);
        }

        [Fact]
        public async Task SearchAsync_ByFacilityNameExact_ReturnsMatchingFacilities()
        {
            var facility = _sampleFacilities[0];
            var model = new FacilitySearchModel { FacilityName = facility.FacilityName, FacilityNameContains = false };
            var results = await _queries.SearchAsync(model);

            Assert.Single(results);
            Assert.Equal(facility.FacilityName, results[0].FacilityName);
        }

        [Fact]
        public async Task SearchAsync_ByFacilityNameContains_ReturnsMatchingFacilities()
        {
            var model = new FacilitySearchModel { FacilityName = "Facility One", FacilityNameContains = true };
            var results = await _queries.SearchAsync(model);

            Assert.Equal(2, results.Count); // "Facility One" and "Another Facility One"
        }

        [Fact]
        public async Task SearchAsync_Combination_ReturnsMatchingFacilities()
        {
            var facility = _sampleFacilities[0];
            var model = new FacilitySearchModel { FacilityId = facility.FacilityId, FacilityName = "Facility", FacilityNameContains = true };
            var results = await _queries.SearchAsync(model);

            Assert.Single(results);
            Assert.Equal(facility.FacilityId, results[0].FacilityId);
        }

        [Fact]
        public async Task SearchAsync_Paged_WithNoCriteria_ReturnsAllPaged()
        {
            var searchModel = new FacilitySearchModel();
            var pageSize = 2;
            var pageNumber = 1;
            var sortBy = "FacilityId";
            var sortOrder = SortOrder.Ascending;

            var pagedFacilities = _sampleFacilities.Take(pageSize).ToList();
            var metadata = new PaginationMetadata { TotalCount = _sampleFacilities.Count, PageSize = pageSize, PageNumber = pageNumber };

            _mockRepository.Setup(r => r.SearchAsync(null, sortBy, sortOrder, pageSize, pageNumber, It.IsAny<CancellationToken>()))
                .ReturnsAsync((pagedFacilities, metadata));

            var result = await _queries.SearchAsync(searchModel, sortBy, sortOrder, pageSize, pageNumber);

            Assert.Equal(pagedFacilities.Count, result.Records.Count);
            Assert.Equal(metadata.TotalCount, result.Metadata.TotalCount);
        }

        [Fact]
        public async Task SearchAsync_Paged_WithCriteria_BuildsPredicateCorrectly()
        {
            var facility = _sampleFacilities[0];
            var searchModel = new FacilitySearchModel { Id = facility.Id, FacilityId = "FAC001", FacilityName = "One", FacilityNameContains = true };
            var pageSize = 1;
            var pageNumber = 1;

            Expression<Func<Facility, bool>> capturedPredicate = null;

            _mockRepository.Setup(r => r.SearchAsync(It.IsAny<Expression<Func<Facility, bool>>>(), null, null, pageSize, pageNumber, It.IsAny<CancellationToken>()))
                .Callback<Expression<Func<Facility, bool>>, string, SortOrder?, int, int, CancellationToken>((p, s, o, ps, pn, ct) => capturedPredicate = p)
                .ReturnsAsync((new List<Facility> { facility }, new PaginationMetadata { TotalCount = 1, PageSize = pageSize, PageNumber = pageNumber }));

            var result = await _queries.SearchAsync(searchModel, null, null, pageSize, pageNumber);

            Assert.NotNull(result);
            Assert.Single(result.Records);
            Assert.True(capturedPredicate.Compile()(facility)); // Verify predicate matches the facility
            Assert.False(capturedPredicate.Compile()(_sampleFacilities[1])); // Does not match another
        }

        [Fact]
        public async Task SearchAsync_Paged_MapsToModelCorrectly()
        {
            var facility = _sampleFacilities[0];
            var searchModel = new FacilitySearchModel();
            var pageSize = 1;
            var pageNumber = 1;

            _mockRepository.Setup(r => r.SearchAsync(null, null, null, pageSize, pageNumber, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<Facility> { facility }, new PaginationMetadata { TotalCount = 1, PageSize = pageSize, PageNumber = pageNumber }));

            var result = await _queries.SearchAsync(searchModel, null, null, pageSize, pageNumber);

            var model = result.Records[0];
            Assert.Equal(facility.Id, model.Id);
            Assert.Equal(facility.FacilityId, model.FacilityId);
            Assert.Equal(facility.FacilityName, model.FacilityName);
            Assert.Equal(facility.TimeZone, model.TimeZone);
            Assert.Equal(facility.ScheduledReports.Daily, model.ScheduledReports.Daily);
            Assert.Equal(facility.ScheduledReports.Weekly, model.ScheduledReports.Weekly);
            Assert.Equal(facility.ScheduledReports.Monthly, model.ScheduledReports.Monthly);
        }
    }
}