using AutoMapper;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Models;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant
{
    [Collection("TenantIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class TenantBusinessTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TenantIntegrationTestFixture _fixture;
        private readonly IFacilityManager _manager;
        private readonly IFacilityQueries _queries;
        private readonly IEntityRepository<Facility> _repo;
        private readonly IMapper _mapper;

        public TenantBusinessTests(TenantIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;

            _manager = _fixture.ServiceProvider.GetRequiredService<IFacilityManager>();
            _queries = _fixture.ServiceProvider.GetRequiredService<IFacilityQueries>();
            _repo = _fixture.ServiceProvider.GetRequiredService<IEntityRepository<Facility>>();
            _mapper = _fixture.ServiceProvider.GetRequiredService<IMapper>();
        }

        [Fact]
        public async Task CreateFacility_Success()
        {
            var facility = new Facility
            {
                FacilityId = "TestFacility1",
                FacilityName = "Test Facility 1",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { },
                    Weekly = new string[] { },
                    Monthly = new string[] { }
                }
            };

            await _manager.CreateAsync(facility, CancellationToken.None);

            var saved = await _queries.GetAsync("TestFacility1", null, CancellationToken.None);

            Assert.NotNull(saved);
            Assert.Equal("TestFacility1", saved.FacilityId);
            Assert.Equal("Test Facility 1", saved.FacilityName);
            Assert.Equal("America/Chicago", saved.TimeZone);

            // Use repo to assert CreateDate
            var entity = await _repo.FirstOrDefaultAsync(x => x.FacilityId == "TestFacility1", CancellationToken.None);
            Assert.NotNull(entity?.CreateDate);
        }

        [Fact]
        public async Task CreateFacility_Duplicate_ThrowsException()
        {
            var facility = new Facility
            {
                FacilityId = "DuplicateFacility",
                FacilityName = "Duplicate Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { },
                    Weekly = new string[] { },
                    Monthly = new string[] { }
                }
            };

            await _manager.CreateAsync(facility, CancellationToken.None);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(facility, CancellationToken.None));
        }


        [Fact]
        public async Task CreateFacility_InvalidCharacters_FacilityId_ThrowsException()
        {
            var facility = new Facility
            {
                FacilityId = "Facility@!#",
                FacilityName = "Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { },
                    Weekly = new string[] { },
                    Monthly = new string[] { }
                }
            };

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(facility, CancellationToken.None));
        }

        [Fact]
        public async Task CreateFacility_InvalidTimeZone_ThrowsException()
        {
            var facility = new Facility
            {
                FacilityId = "InvalidTimeZoneFacility",
                FacilityName = "Invalid TimeZone Facility",
                TimeZone = "Invalid/TimeZone",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { },
                    Weekly = new string[] { },
                    Monthly = new string[] { }
                }
            };

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(facility, CancellationToken.None));
            Assert.Contains("Timezone Not Found", ex.Message);
        }

        [Fact]
        public async Task CreateFacility_DuplicateReports_ThrowsException()
        {
            var facility = new Facility
            {
                FacilityId = "DuplicateReportsFacility",
                FacilityName = "Duplicate Reports Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { "ReportA" },
                    Weekly = new string[] { "ReportA" },
                    Monthly = new string[] { }
                }
            };

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(facility, CancellationToken.None));
            Assert.Contains("Duplicate entries found", ex.Message);
        }

        [Fact]
        public async Task SearchFacilities_Success()
        {
            var facility1 = new Facility
            {
                FacilityId = "GetAllFacility1",
                FacilityName = "GetAll Facility 1",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };
            var facility2 = new Facility
            {
                FacilityId = "GetAllFacility2",
                FacilityName = "GetAll Facility 2",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            await _manager.CreateAsync(facility1, CancellationToken.None);
            await _manager.CreateAsync(facility2, CancellationToken.None);

            var paged = await _queries.PagedSearchAsync(new FacilitySearchModel(), pageSize: 100,  pageNumber: 1,  cancellationToken: CancellationToken.None);
            var allFacilities = paged.Records;

            Assert.True(allFacilities.Count >= 2);
            Assert.Contains(allFacilities, f => f.FacilityId == "GetAllFacility1");
            Assert.Contains(allFacilities, f => f.FacilityId == "GetAllFacility2");
        }

        [Fact]
        public async Task GetFacilityById_Success()
        {
            var facility = new Facility
            {
                FacilityId = "GetByIdFacility",
                FacilityName = "GetById Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            await _manager.CreateAsync(facility, CancellationToken.None);

            var saved = await _queries.GetAsync("GetByIdFacility", null, CancellationToken.None);
            Assert.NotNull(saved);

            var byId = await _queries.GetAsync(saved.Id!.Value, CancellationToken.None);
            Assert.NotNull(byId);
            Assert.Equal(saved.Id, byId.Id);
            Assert.Equal("GetByIdFacility", byId.FacilityId);
        }

        [Fact]
        public async Task UpdateFacility_Success()
        {
            var facility = new Facility
            {
                FacilityId = "UpdateFacility",
                FacilityName = "Original Name",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            await _manager.CreateAsync(facility, CancellationToken.None);

            var saved = await _queries.GetAsync("UpdateFacility", null, CancellationToken.None);
            Assert.NotNull(saved);

            var updatedFacility = new Facility
            {
                FacilityId = "UpdateFacility",
                FacilityName = "Updated Name",
                TimeZone = "America/New_York",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { "NewReport" }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            var updateResult = await _manager.UpdateAsync(saved.Id!.Value, updatedFacility, CancellationToken.None);
            Assert.Equal(saved.Id.ToString(), updateResult);

            var updated = await _queries.GetAsync(saved.Id!.Value, CancellationToken.None);
            Assert.Equal("Updated Name", updated.FacilityName);
            Assert.Equal("America/New_York", updated.TimeZone);
            Assert.Contains("NewReport", updated.ScheduledReports.Daily);
        }

        [Fact]
        public async Task RemoveFacility_Success()
        {
            var facility = new Facility
            {
                FacilityId = "RemoveFacility",
                FacilityName = "Remove Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            await _manager.CreateAsync(facility, CancellationToken.None);

            var saved = await _queries.GetAsync("RemoveFacility");
            Assert.NotNull(saved);

            var removeResult = await _manager.DeleteAsync("RemoveFacility", CancellationToken.None);
            Assert.Equal("RemoveFacility", removeResult);

            var deleted = await _queries.GetAsync("RemoveFacility");
            Assert.Null(deleted);
        }

        [Fact]
        public async Task RemoveFacility_NotFound_ThrowsException()
        {
            await Assert.ThrowsAsync<ApplicationException>(() => _manager.DeleteAsync("NonExistentFacility", CancellationToken.None));
        }
    }
}