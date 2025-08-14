// FacilityConfigurationServiceTests.cs
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant
{
    [Collection("TenantIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class FacilityConfigurationServiceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TenantIntegrationTestFixture _fixture;
        private readonly IFacilityConfigurationService _service;
        private readonly IEntityRepository<Facility> _repo;

        public FacilityConfigurationServiceTests(TenantIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;

            _service = _fixture.ServiceProvider.GetRequiredService<IFacilityConfigurationService>();
            _repo = _fixture.ServiceProvider.GetRequiredService<IEntityRepository<Facility>>();
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

            await _service.CreateFacility(facility, CancellationToken.None);

            var saved = await _service.GetFacilityByFacilityId("TestFacility1", CancellationToken.None);

            Assert.NotNull(saved);
            Assert.Equal("TestFacility1", saved.FacilityId);
            Assert.Equal("Test Facility 1", saved.FacilityName);
            Assert.Equal("America/Chicago", saved.TimeZone);
            Assert.NotNull(saved.CreateDate);
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

            await _service.CreateFacility(facility, CancellationToken.None);

            await Assert.ThrowsAsync<ApplicationException>(() => _service.CreateFacility(facility, CancellationToken.None));
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

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _service.CreateFacility(facility, CancellationToken.None));
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

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _service.CreateFacility(facility, CancellationToken.None));
            Assert.Contains("Duplicate entries found", ex.Message);
        }

        [Fact]
        public async Task GetAllFacilities_Success()
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

            await _service.CreateFacility(facility1, CancellationToken.None);
            await _service.CreateFacility(facility2, CancellationToken.None);

            var allFacilities = await _service.GetAllFacilities(CancellationToken.None);

            Assert.Equal(2, allFacilities.Count);
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

            await _service.CreateFacility(facility, CancellationToken.None);

            var saved = await _service.GetFacilityByFacilityId("GetByIdFacility", CancellationToken.None);
            Assert.NotNull(saved);

            var byId = await _service.GetFacilityById(saved.Id, CancellationToken.None);
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

            await _service.CreateFacility(facility, CancellationToken.None);

            var saved = await _service.GetFacilityByFacilityId("UpdateFacility", CancellationToken.None);
            Assert.NotNull(saved);

            var updatedFacility = new Facility
            {
                FacilityId = "UpdateFacility",
                FacilityName = "Updated Name",
                TimeZone = "America/New_York",
                ScheduledReports = new ScheduledReportModel { Daily = new string[] { "NewReport" }, Weekly = new string[] { }, Monthly = new string[] { } }
            };

            var updateResult = await _service.UpdateFacility(saved.Id, updatedFacility, CancellationToken.None);
            Assert.NotNull(updateResult);

            var updated = await _service.GetFacilityById(saved.Id, CancellationToken.None);
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

            await _service.CreateFacility(facility, CancellationToken.None);

            var saved = await _service.GetFacilityByFacilityId("RemoveFacility", CancellationToken.None);
            Assert.NotNull(saved);

            var removeResult = await _service.RemoveFacility("RemoveFacility", CancellationToken.None);
            Assert.Equal("RemoveFacility", removeResult);

            var deleted = await _service.GetFacilityByFacilityId("RemoveFacility", CancellationToken.None);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task RemoveFacility_NotFound_ThrowsException()
        {
            await Assert.ThrowsAsync<ApplicationException>(() => _service.RemoveFacility("NonExistentFacility", CancellationToken.None));
        }
    }
}