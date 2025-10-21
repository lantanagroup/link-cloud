using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Models;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Moq;
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
        public async Task SearchAsync_WithNoCriteria_ReturnsAllFacilities()
        {
            var model = new FacilitySearchModel();
            var results = await _queries.SearchAsync(model);

            Assert.Equal(3, results.Count);
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
        public async Task PagedSearchAsync_WithNoCriteria_ReturnsPagedFacilities()
        {
            var searchModel = new FacilitySearchModel();
            var pageSize = 2;
            var pageNumber = 1;
            var sortBy = "FacilityId";
            var sortOrder = SortOrder.Ascending;

            var result = await _queries.PagedSearchAsync(searchModel, sortBy, sortOrder, pageSize, pageNumber);

            Assert.Equal(2, result.Records.Count);
            Assert.Equal(_sampleFacilities.Count, result.Metadata.TotalCount);
            Assert.Equal(pageSize, result.Metadata.PageSize);
            Assert.Equal(pageNumber, result.Metadata.PageNumber);
            Assert.Equal("FAC001", result.Records[0].FacilityId); // Assuming ascending sort by FacilityId
            Assert.Equal("FAC002", result.Records[1].FacilityId);
        }

        [Fact]
        public async Task PagedSearchAsync_WithCriteria_ReturnsMatchingPagedFacilities()
        {
            var facility = _sampleFacilities[0];
            var searchModel = new FacilitySearchModel { Id = facility.Id, FacilityId = "FAC001", FacilityName = "One", FacilityNameContains = true };
            var pageSize = 1;
            var pageNumber = 1;

            var result = await _queries.PagedSearchAsync(searchModel, pageSize: pageSize, pageNumber: pageNumber);

            Assert.NotNull(result);
            Assert.Single(result.Records);
            Assert.Equal(facility.Id, result.Records[0].Id);
            Assert.Equal(facility.FacilityId, result.Records[0].FacilityId);
            Assert.Equal(facility.FacilityName, result.Records[0].FacilityName);
        }

        [Fact]
        public async Task PagedSearchAsync_MapsToModelCorrectly()
        {
            var facility = _sampleFacilities[0];
            var searchModel = new FacilitySearchModel { Id = facility.Id };
            var pageSize = 1;
            var pageNumber = 1;

            var result = await _queries.PagedSearchAsync(searchModel, pageSize: pageSize, pageNumber: pageNumber);

            var model = result.Records[0];
            Assert.Equal(facility.Id, model.Id);
            Assert.Equal(facility.FacilityId, model.FacilityId);
            Assert.Equal(facility.FacilityName, model.FacilityName);
        }
    }
}