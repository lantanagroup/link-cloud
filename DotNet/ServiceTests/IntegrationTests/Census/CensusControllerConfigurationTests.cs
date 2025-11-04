using Census.Controllers;
using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Domain.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census
{
    [Collection("CensusIntegrationTests")]
    public class CensusConfigControllerTests : IClassFixture<CensusIntegrationTestFixture>
    {
        private readonly CensusIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public CensusConfigControllerTests(CensusIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        [Fact]
        public async Task AddCensusConfig_WithNoFlag_ShouldCreateConfigWithEnabledTrue()
        {
            // Arrange
            var controller = _fixture.ServiceProvider
                .GetRequiredService<CensusConfigController>();
            var db = _fixture.DbContext;
            var scheduler = _fixture.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;

            var facilityId = "TestFacilityNoFlag" + Guid.NewGuid().ToString();
            var model = new CensusConfigModel
            {
                FacilityId = facilityId,
                ScheduledTrigger = "0 0 * * * ?"
            };

            try
            {
                // Act
                var result = await controller.Create(model);

                // Assert
                Assert.NotNull(result);
                var createdResult = Assert.IsType<CreatedResult>(result);
                var returnValue = Assert.IsType<CensusConfigModel>(createdResult.Value);
                Assert.Equal(facilityId, returnValue.FacilityId);
                Assert.Equal(model.ScheduledTrigger, returnValue.ScheduledTrigger);
                Assert.True(returnValue.Enabled); // Default value should be true

                // Check if the job exists in the scheduler
                var jobKey = new JobKey($"{facilityId}-PatientCensusScheduled", "PatientCensusScheduled");
                var jobExists = await scheduler.CheckExists(jobKey);
                Assert.True(jobExists, "Job should be created when enabled flag is true (default)");
            }
            finally
            {
                // Cleanup
                var config = await db.CensusConfigs.FindAsync(facilityId);
                if (config != null)
                {
                    db.CensusConfigs.Remove(config);
                    await db.SaveChangesAsync();
                }

                var jobKey = new JobKey($"{facilityId}-PatientCensusScheduled", "PatientCensusScheduled");
                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.DeleteJob(jobKey);
                }
            }
        }

        [Fact]
        public async Task AddCensusConfig_WithFlagSetToFalse_ShouldCreateConfigWithNoJob()
        {
            // Arrange
            var controller = _fixture.ServiceProvider
                .GetRequiredService<CensusConfigController>();
            var db = _fixture.DbContext;
            var scheduler = _fixture.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;

            var facilityId = "TestFacilityDisabled" + Guid.NewGuid().ToString();
            var model = new CensusConfigModel
            {
                FacilityId = facilityId,
                ScheduledTrigger = "0 0 * * * ?",
                Enabled = false
            };

            try
            {
                // Act
                var result = await controller.Create(model);

                // Assert
                Assert.NotNull(result);
                var createdResult = Assert.IsType<CreatedResult>(result);
                var returnValue = Assert.IsType<CensusConfigModel>(createdResult.Value);
                Assert.Equal(facilityId, returnValue.FacilityId);
                Assert.Equal(model.ScheduledTrigger, returnValue.ScheduledTrigger);
                Assert.False(returnValue.Enabled);

                // Check that the job doesn't exist in the scheduler
                var jobKey = new JobKey($"{facilityId}-PatientCensusScheduled", "PatientCensusScheduled");
                var jobExists = await scheduler.CheckExists(jobKey);
                Assert.False(jobExists, "Job should not be created when enabled flag is false");
            }
            finally
            {
                // Cleanup
                var config = await db.CensusConfigs.FindAsync(facilityId);
                if (config != null)
                {
                    db.CensusConfigs.Remove(config);
                    await db.SaveChangesAsync();
                }
            }
        }

        [Fact]
        public async Task UpdateCensusConfig_FromEnabledToDisabled_ShouldRemoveScheduledJob()
        {
            // Arrange
            var controller = _fixture.ServiceProvider
                .GetRequiredService<CensusConfigController>();
            var db = _fixture.DbContext;
            var scheduler = _fixture.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;

            var facilityId = "TestFacilityToggle" + Guid.NewGuid().ToString();

            // First create an enabled config
            var createModel = new CensusConfigModel
            {
                FacilityId = facilityId,
                ScheduledTrigger = "0 0 * * * ?",
                Enabled = true
            };

            try
            {
                // Create the config with enabled=true
                var createResult = await controller.Create(createModel);
                Assert.NotNull(createResult);

                // Verify the job exists
                var jobKey = new JobKey($"{facilityId}-PatientCensusScheduled", "PatientCensusScheduled");
                var jobExistsBefore = await scheduler.CheckExists(jobKey);
                Assert.True(jobExistsBefore, "Job should be created when enabled flag is true");

                // Now update to disabled
                var updateModel = new CensusConfigModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "0 0 * * * ?",
                    Enabled = false
                };

                // Act
                var updateResult = await controller.Put(updateModel, facilityId);

                // Assert
                Assert.NotNull(updateResult);
                // Check that we have a result
                Assert.NotNull(updateResult.Result);
                Assert.IsType<AcceptedResult>(updateResult.Result);

                // Extract the value from the AcceptedResult
                var acceptedResult = (AcceptedResult)updateResult.Result;
                Assert.NotNull(acceptedResult.Value);

                // Verify the value is of the expected type
                var returnValue = Assert.IsType<CensusConfigModel>(acceptedResult.Value);
                Assert.Equal(facilityId, returnValue.FacilityId);
                Assert.False(returnValue.Enabled);


                // Verify the job no longer exists
                var jobExistsAfter = await scheduler.CheckExists(jobKey);
                Assert.False(jobExistsAfter, "Job should be removed when enabled flag is set to false");
            }
            finally
            {
                // Cleanup
                var config = await db.CensusConfigs.FindAsync(facilityId);
                if (config != null)
                {
                    db.CensusConfigs.Remove(config);
                    await db.SaveChangesAsync();
                }

                var jobKey = new JobKey($"{facilityId}-PatientCensusScheduled", "PatientCensusScheduled");
                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.DeleteJob(jobKey);
                }
            }
        }

        [Fact]
        public async Task UpdateCensusConfig_FromDisabledToEnabled_ShouldCreateScheduledJob()
        {
            // Arrange
            var controller = _fixture.ServiceProvider
                .GetRequiredService<CensusConfigController>();
            var db = _fixture.DbContext;
            var scheduler = _fixture.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;

            var facilityId = "TestFacilityReEnable" + Guid.NewGuid().ToString();

            // First create a disabled config
            var createModel = new CensusConfigModel
            {
                FacilityId = facilityId,
                ScheduledTrigger = "0 0 * * * ?",
                Enabled = false
            };

            try
            {
                // Create the config with enabled=false
                var createResult = await controller.Create(createModel);
                Assert.NotNull(createResult);
                //assert that flag is false
                var createdResult = Assert.IsType<CreatedResult>(createResult);
                var createdEntity = Assert.IsType<CensusConfigModel>(createdResult.Value);
                Assert.False(createdEntity.Enabled, "The Created model should have Enabled set to false");
                

                // Verify no job exists
                var jobKey = new JobKey($"{facilityId}-PatientCensusScheduled", "PatientCensusScheduled");
                var jobExistsBefore = await scheduler.CheckExists(jobKey);
                Assert.False(jobExistsBefore, "Job should not be created when enabled flag is false");

                // Now update to enabled
                var updateModel = new CensusConfigModel
                {
                    FacilityId = facilityId,
                    ScheduledTrigger = "0 0 * * * ?",
                    Enabled = true
                };

                // Act
                var updateResult = await controller.Put(updateModel, facilityId);

                // Assert
                Assert.NotNull(updateResult);
                // Check that we have a result
                Assert.NotNull(updateResult.Result);
                Assert.IsType<AcceptedResult>(updateResult.Result);

                // Extract the value from the AcceptedResult
                var acceptedResult = (AcceptedResult)updateResult.Result;
                Assert.NotNull(acceptedResult.Value);

                // Verify the value is of the expected type
                var returnValue = Assert.IsType<CensusConfigModel>(acceptedResult.Value);
                Assert.Equal(facilityId, returnValue.FacilityId);
                Assert.True(returnValue.Enabled);


                // Verify the job now exists
                var jobExistsAfter = await scheduler.CheckExists(jobKey);
                Assert.True(jobExistsAfter, "Job should be created when enabled flag is set to true");
            }
            finally
            {
                // Cleanup
                var config = await db.CensusConfigs.FindAsync(facilityId);
                if (config != null)
                {
                    db.CensusConfigs.Remove(config);
                    await db.SaveChangesAsync();
                }

                var jobKey = new JobKey($"{facilityId}-PatientCensusScheduled", "PatientCensusScheduled");
                if (await scheduler.CheckExists(jobKey))
                {
                    await scheduler.DeleteJob(jobKey);
                }
            }
        }

        [Fact]
        public async Task GetCensusConfig_WithEnabledFlag_ShouldReturnCorrectEnabledValue()
        {
            // Arrange
            var controller = _fixture.ServiceProvider
                .GetRequiredService<CensusConfigController>();
            var db = _fixture.DbContext;
            var scheduler = _fixture.ServiceProvider.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;

            var facilityId = "TestFacilityGet" + Guid.NewGuid().ToString();

            // Add a config directly to the database
            var configEntity = new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 * * * ?",
                Enabled = false,
                Id = Guid.NewGuid().ToString(),
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            };

            try
            {
                db.CensusConfigs.Add(configEntity);
                await db.SaveChangesAsync();

                // Act
                var result = await controller.Get(facilityId);

                // Assert
                Assert.NotNull(result);

                // Get the ActionResult's actual result (which is an OkObjectResult)
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                Assert.Equal(200, okResult.StatusCode);

                // Then get the model from the OkObjectResult
                var returnValue = Assert.IsType<CensusConfigModel>(okResult.Value);
                Assert.Equal(facilityId, returnValue.FacilityId);
                Assert.False(returnValue.Enabled); // Should match what we set

            }
            finally
            {
                // Cleanup
                var config = await db.CensusConfigs.FindAsync(facilityId);
                if (config != null)
                {
                    db.CensusConfigs.Remove(config);
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}