using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Commands;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Quartz;

namespace CensusUnitTests
{

    public class CRUDCommandTests
    {
        //private ILogger<CreateCensusConfigCommand> _logger;
        private IOptions<MongoConnection>? _mongoSettings;
        private Mock<ISchedulerFactory>? _schedulerFactoryMock;
        private Mock<ScheduleService>? _scheduleServiceMock;

        [Fact]
        public void Setup()
        {
            _mongoSettings = Mock.Of<IOptions<MongoConnection>>();
            _schedulerFactoryMock = new Mock<ISchedulerFactory>();
            _scheduleServiceMock = new Mock<ScheduleService>();
        }

        [Fact]
        public async Task CreateCensusConfig_Success() 
        {
            Setup();
            var testCase = new CensusConfigEntity
            {
                FacilityID = "TestFacility_001",
                ScheduledTrigger = "0 0 0 * * ?"
            };
            var logger = Mock.Of<ILogger<CreateCensusConfigCommandHandler>>();
            var censusConfigServiceMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediator = new Mock<IMediator>();

            var mocker = new AutoMocker();
            var settings = mocker.CreateInstance<TenantServiceRegistration>();
            settings.CheckIfTenantExists = false;
            mocker.Use(settings);
            var tenantApiServiceMock = new Mock<ITenantApiService>();
            

            censusConfigServiceMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(testCase));
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            tenantApiServiceMock.Setup(x => x.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediator.Object, tenantApiServiceMock.Object);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new CensusConfigModel
                    {
                        FacilityId = testCase.FacilityID,
                        ScheduledTrigger = testCase.ScheduledTrigger
                    }
                }, new CancellationToken());
                Assert.True(true);
            }
            catch (Exception)
            {
                Assert.False(true);
            }
        }

        [Fact]
        public async Task CreateCensusConfig_SchedulerFail() 
        {
            Setup();
            var testCase = new CensusConfigEntity
            {
                FacilityID = "TestFacility_001",
                ScheduledTrigger = "0 0 0 * * ?"
            };
            var logger = Mock.Of<ILogger<CreateCensusConfigCommandHandler>>();
            var censusConfigServiceMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediator = new Mock<IMediator>();

            var mocker = new AutoMocker();
            var settings = mocker.CreateInstance<TenantServiceRegistration>();
            settings.CheckIfTenantExists = false;
            mocker.Use(settings);
            var tenantApiService = (ITenantApiService)mocker.CreateInstance<TenantApiService>();
            mocker.Use(tenantApiService);

            censusConfigServiceMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(testCase));
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(testCase);
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Throws(new Exception());

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediator.Object, tenantApiService);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new CensusConfigModel
                    {
                        FacilityId = testCase.FacilityID,
                        ScheduledTrigger = testCase.ScheduledTrigger
                    }
                }, new CancellationToken());
                Assert.False(true);
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }

        [Fact]
        public async Task CreateCensusConfig_UpdateFail() 
        {
            Setup();
            var testCase = new CensusConfigEntity
            {
                FacilityID = "TestFacility_001",
                ScheduledTrigger = "0 0 0 * * ?"
            };
            var logger = Mock.Of<ILogger<CreateCensusConfigCommandHandler>>();
            var censusConfigServiceMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediator = new Mock<IMediator>();


            var mocker = new AutoMocker();
            var settings = mocker.CreateInstance<TenantServiceRegistration>();
            settings.CheckIfTenantExists = false;
            mocker.Use(settings);
            var tenantApiServiceMock = new Mock<ITenantApiService>();

            censusConfigServiceMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns(Task.FromResult(testCase));
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            tenantApiServiceMock.Setup(x => x.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediator.Object, tenantApiServiceMock.Object);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new CensusConfigModel
                    {
                        FacilityId = testCase.FacilityID,
                        ScheduledTrigger = testCase.ScheduledTrigger
                    }
                }, new CancellationToken());
                Assert.True(true);
            }
            catch (Exception)
            {
                Assert.False(true);
            }
        }

        [Fact]
        public async Task CreateCensusConfig_AddFail() 
        {
            Setup();
            var testCase = new CensusConfigEntity
            {
                FacilityID = "TestFacility_001",
                ScheduledTrigger = "0 0 0 * * ?"
            };
            var logger = Mock.Of<ILogger<CreateCensusConfigCommandHandler>>();
            var censusConfigServiceMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediator = new Mock<IMediator>();

            var mocker = new AutoMocker();
            var settings = mocker.CreateInstance<TenantServiceRegistration>();
            settings.CheckIfTenantExists = false;
            mocker.Use(settings);
            var tenantApiServiceMock = new Mock<ITenantApiService>();

            censusConfigServiceMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(testCase));
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            tenantApiServiceMock.Setup(x => x.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediator.Object, tenantApiServiceMock.Object);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new CensusConfigModel
                    {
                        FacilityId = testCase.FacilityID,
                        ScheduledTrigger = testCase.ScheduledTrigger
                    }
                }, new CancellationToken());
                Assert.True(true);
            }
            catch (Exception)
            {
                Assert.False(true);
            }
        }

        [Fact]
        public async Task DeleteConfig_Success() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<DeleteCensusConfigCommandHandler>>();
            var schedulingFactoryMock = new Mock<ISchedulerFactory>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();

            censusConfigMongoRepoMock.Setup(x => x.RemoveByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(Setup => Setup.DeleteJobsForFacility(It.IsAny<string>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new DeleteCensusConfigCommandHandler(logger, censusConfigMongoRepoMock.Object, schedulingFactoryMock.Object, censusSchedulingRepoMock.Object);

            try
            {
                await handler.Handle(new DeleteCensusConfigCommand
                {
                    FacilityId = facilityId
                }, new CancellationToken());
                Assert.True(true);
            }
            catch(Exception)
            {

                Assert.True(false);
            }
        }

        [Fact]
        public async Task DeleteConfig_MongoFail() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<DeleteCensusConfigCommandHandler>>();
            var schedulingFactoryMock = new Mock<ISchedulerFactory>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();

            censusConfigMongoRepoMock.Setup(x => x.RemoveByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            censusSchedulingRepoMock.Setup(Setup => Setup.DeleteJobsForFacility(It.IsAny<string>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new DeleteCensusConfigCommandHandler(logger, censusConfigMongoRepoMock.Object, schedulingFactoryMock.Object, censusSchedulingRepoMock.Object);

            try
            {
                await handler.Handle(new DeleteCensusConfigCommand
                {
                    FacilityId = facilityId
                }, new CancellationToken());
                Assert.True(false);
            }
            catch (Exception)
            {

                Assert.True(true);
            }
        }

        [Fact]
        public async Task DeleteConfig_SchedulerFail() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<DeleteCensusConfigCommandHandler>>();
            var schedulingFactoryMock = new Mock<ISchedulerFactory>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();

            censusConfigMongoRepoMock.Setup(x => x.RemoveByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(Setup => Setup.DeleteJobsForFacility(It.IsAny<string>(), It.IsAny<IScheduler>())).Throws(new Exception());

            var handler = new DeleteCensusConfigCommandHandler(logger, censusConfigMongoRepoMock.Object, schedulingFactoryMock.Object, censusSchedulingRepoMock.Object);

            try
            {
                await handler.Handle(new DeleteCensusConfigCommand
                {
                    FacilityId = facilityId
                }, new CancellationToken());
                Assert.True(false);
            }
            catch (Exception)
            {

                Assert.True(true);
            }
        }

        [Fact]
        public async Task GetCensusConfig_Success() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<GetCensusConfigQueryHandler>>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();

            censusConfigMongoRepoMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));

            var handler = new GetCensusConfigQueryHandler(logger, censusConfigMongoRepoMock.Object);

            try
            {
                var response = await handler.Handle(new GetCensusConfigQuery
                {
                    FacilityId = facilityId
                }, new CancellationToken());
                Assert.True(true);
            }
            catch (Exception)
            {

                Assert.True(false);
            }
        }

        [Fact]
        public async Task GetCensusConfig_Null() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<GetCensusConfigQueryHandler>>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();

            censusConfigMongoRepoMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult((CensusConfigEntity)null));

            var handler = new GetCensusConfigQueryHandler(logger, censusConfigMongoRepoMock.Object);

            try
            {
                var response = await handler.Handle(new GetCensusConfigQuery
                {
                    FacilityId = facilityId
                }, new CancellationToken());
                
                if(response == null)
                    Assert.True(true);
                else
                    Assert.True(false);
            }
            catch (Exception)
            {
                Assert.True(false);
            }
        }

        [Fact]
        public async Task UpdateCensusConfig_Success() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<UpdateCensusCommandHandler>>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediatorMock = new Mock<IMediator>();

            var mocker = new AutoMocker();
            var settings = mocker.CreateInstance<TenantServiceRegistration>();
            settings.CheckIfTenantExists = false;
            mocker.Use(settings);
            var tenantApiServiceMock = new Mock<ITenantApiService>();

            censusConfigMongoRepoMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));
            censusConfigMongoRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusConfigEntity());
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            tenantApiServiceMock.Setup(x => x.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));


            var handler = new UpdateCensusCommandHandler(logger, censusConfigMongoRepoMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediatorMock.Object, tenantApiServiceMock.Object);

            try
            {
                var response = await handler.Handle(new UpdateCensusConfigCommand
                {
                    Config = new CensusConfigModel
                    {
                        FacilityId = facilityId,
                        ScheduledTrigger = "0 0 0 * * ?"
                    }
                }, new CancellationToken());
                Assert.True(true);
            }
            catch (Exception)
            {
                Assert.True(false);
            }
        }

        [Fact]
        public async Task UpdateCensusConfig_SchedulerFail() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<UpdateCensusCommandHandler>>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediatorMock = new Mock<IMediator>();
            var tenant = new Mock<ITenantApiService>();

            censusConfigMongoRepoMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));
            censusConfigMongoRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusConfigEntity());
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Throws(new Exception());

            var handler = new UpdateCensusCommandHandler(logger, censusConfigMongoRepoMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediatorMock.Object, tenant.Object);

            try
            {
                var response = await handler.Handle(new UpdateCensusConfigCommand
                {
                    Config = new CensusConfigModel
                    {
                        FacilityId = facilityId,
                        ScheduledTrigger = "0 0 0 * * ?"
                    }
                }, new CancellationToken());
                Assert.True(false);
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }

        [Fact]
        public async Task UpdateCensusConfig_NoExistingFacility() 
        {
            Setup();
            var facilityId = "TestFacility_001";

            var logger = Mock.Of<ILogger<UpdateCensusCommandHandler>>();
            var censusConfigMongoRepoMock = new Mock<ICensusConfigRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediatorMock = new Mock<IMediator>();
            var tenant = new Mock<ITenantApiService>();

            censusConfigMongoRepoMock.Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusConfigEntity());
            censusConfigMongoRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusConfigEntity());
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new UpdateCensusCommandHandler(logger, censusConfigMongoRepoMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediatorMock.Object, tenant.Object);

            try
            {
                var response = await handler.Handle(new UpdateCensusConfigCommand
                {
                    Config = new CensusConfigModel
                    {
                        FacilityId = facilityId,
                        ScheduledTrigger = "0 0 0 * * ?"
                    }
                }, new CancellationToken());
                Assert.True(false);
            }
            catch (Exception)
            {
                Assert.True(true);
            }
        }
    }
}
