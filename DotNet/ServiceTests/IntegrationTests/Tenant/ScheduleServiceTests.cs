using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Jobs;
using LantanaGroup.Link.Tenant.Repository.Context;
using LantanaGroup.Link.Tenant.Services;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.Matchers;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Tenant
{
    [Collection("TenantIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ScheduleServiceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TenantIntegrationTestFixture _fixture;
        private readonly ScheduleService _service;
        private readonly TenantDbContext _dbContext;
        private readonly ISchedulerFactory _schedulerFactory;

        public ScheduleServiceTests(TenantIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;

            _service = _fixture.ServiceProvider.GetRequiredService<ScheduleService>();
            _service.StartAsync(CancellationToken.None).Wait();
            _dbContext = _fixture.ServiceProvider.GetRequiredService<TenantDbContext>();
            _schedulerFactory = _fixture.ServiceProvider.GetRequiredService<ISchedulerFactory>();
        }

        [Fact]
        public async Task StartAsync_LoadsFacilitiesAndAddsJobs()
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.Clear();

            _dbContext.Facilities.RemoveRange(_dbContext.Facilities.ToList());
            await _dbContext.SaveChangesAsync();

            // Seed facilities
            var facility1 = new Facility
            {
                FacilityId = "FacilityWithDaily",
                FacilityName = "Daily Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { "Report1" },
                    Weekly = new string[] { },
                    Monthly = new string[] { }
                }
            };
            _dbContext.Facilities.Add(facility1);

            var facility2 = new Facility
            {
                FacilityId = "FacilityWithWeeklyMonthly",
                FacilityName = "Weekly Monthly Facility",
                TimeZone = "America/New_York",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { },
                    Weekly = new string[] { "Report2" },
                    Monthly = new string[] { "Report3" }
                }
            };
            _dbContext.Facilities.Add(facility2);

            await _dbContext.SaveChangesAsync();

            // Start the service
            await _service.StartAsync(CancellationToken.None);

            // Verify jobs are added
            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(KafkaTopic.ReportScheduled)));

            Assert.Equal(3, jobKeys.Count); // One for daily, one for weekly, one for monthly

            // Check specific job
            var dailyJobKey = new JobKey("FacilityWithDaily-Daily", nameof(KafkaTopic.ReportScheduled));
            var dailyJob = await scheduler.GetJobDetail(dailyJobKey);
            Assert.NotNull(dailyJob);
            Assert.Equal(typeof(ReportScheduledJob), dailyJob.JobType);
        }

        [Fact]
        public async Task AddJobsForFacility_CreatesJobsForFrequencies()
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.Clear();

            _dbContext.Facilities.RemoveRange(_dbContext.Facilities.ToList());
            await _dbContext.SaveChangesAsync();

            var facility = new Facility
            {
                FacilityId = "AddJobsFacility",
                FacilityName = "Add Jobs Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { "DailyReport" },
                    Weekly = new string[] { "WeeklyReport" },
                    Monthly = new string[] { "MonthlyReport" }
                }
            };

            await _service.AddJobsForFacility(facility, CancellationToken.None);

            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(KafkaTopic.ReportScheduled)));

            Assert.Equal(3, jobKeys.Count);
            Assert.Contains(jobKeys, j => j.Name == "AddJobsFacility-Daily");
            Assert.Contains(jobKeys, j => j.Name == "AddJobsFacility-Weekly");
            Assert.Contains(jobKeys, j => j.Name == "AddJobsFacility-Monthly");
        }

        [Fact]
        public async Task DeleteJobsForFacility_DeletesSpecifiedJobs()
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.Clear();

            _dbContext.Facilities.RemoveRange(_dbContext.Facilities.ToList());
            await _dbContext.SaveChangesAsync();

            var facility = new Facility
            {
                FacilityId = "DeleteJobsFacility",
                FacilityName = "Delete Jobs Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { "Daily" },
                    Weekly = new string[] { "Weekly" },
                    Monthly = new string[] { "Monthly" }
                }
            };

            await _service.AddJobsForFacility(facility, CancellationToken.None);

            // Delete daily and weekly
            await _service.DeleteJobsForFacility("DeleteJobsFacility", new List<string> { "Daily", "Weekly" }, CancellationToken.None);

            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(KafkaTopic.ReportScheduled)));

            Assert.Single(jobKeys);
            Assert.Equal("DeleteJobsFacility-Monthly", jobKeys.First().Name);
        }

        [Fact]
        public async Task UpdateJobsForFacility_UpdatesChangedFrequencies()
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.Clear();

            _dbContext.Facilities.RemoveRange(_dbContext.Facilities.ToList());
            await _dbContext.SaveChangesAsync();

            var existingFacility = new Facility
            {
                FacilityId = "UpdateJobsFacility",
                FacilityName = "Update Jobs Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { "OldDaily" },
                    Weekly = new string[] { "OldWeekly" },
                    Monthly = new string[] { "Monthly" }
                }
            };

            await _service.AddJobsForFacility(existingFacility, CancellationToken.None);

            var updatedFacility = new Facility
            {
                FacilityId = "UpdateJobsFacility",
                FacilityName = "Update Jobs Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { "NewDaily" }, // Changed
                    Weekly = new string[] { }, // Removed
                    Monthly = new string[] { "Monthly" } // Unchanged
                }
            };

            await _service.UpdateJobsForFacility(updatedFacility, existingFacility, CancellationToken.None);

            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(KafkaTopic.ReportScheduled)));

            Assert.Equal(2, jobKeys.Count); // Daily (updated), Monthly (unchanged)
            Assert.Contains(jobKeys, j => j.Name == "UpdateJobsFacility-Daily");
            Assert.Contains(jobKeys, j => j.Name == "UpdateJobsFacility-Monthly");
            Assert.DoesNotContain(jobKeys, j => j.Name == "UpdateJobsFacility-Weekly");
        }

        [Fact]
        public async Task CreateJobAndTrigger_UsesCorrectCronAndTimeZone()
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.Clear();

            _dbContext.Facilities.RemoveRange(_dbContext.Facilities.ToList());
            await _dbContext.SaveChangesAsync();

            var facility = new Facility
            {
                FacilityId = "CronFacility",
                FacilityName = "Cron Facility",
                TimeZone = "America/Chicago",
                ScheduledReports = new ScheduledReportModel
                {
                    Daily = new string[] { },
                    Weekly = new string[] { },
                    Monthly = new string[] { "MonthlyReport" }
                }
            };

            await _service.AddJobsForFacility(facility, CancellationToken.None);

            var jobKey = new JobKey("CronFacility-Monthly", nameof(KafkaTopic.ReportScheduled));
            var triggers = await scheduler.GetTriggersOfJob(jobKey);

            var trigger = triggers.First() as ICronTrigger;
            Assert.NotNull(trigger);
            Assert.Equal("0 0 0 1 * ? *", trigger.CronExpressionString);
            Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"), trigger.TimeZone);
        }
    }
}