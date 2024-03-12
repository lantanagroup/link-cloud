using Census.Commands;
using Census.Domain.Entities;
using Census.Models;
using Census.Repositories;
using Census.Services;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Commands;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;

namespace CensusUnitTests
{

    public class CRUDCommandTests
    {
        //private ILogger<CreateCensusConfigCommand> _logger;
        private IOptions<MongoConnection> _mongoSettings;
        private Mock<ISchedulerFactory> _schedulerFactoryMock;
        private Mock<ScheduleService> _scheduleServiceMock;

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
            var censusConfigServiceMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();


            censusConfigServiceMock.Setup(x => x.Get(It.IsAny<string>())).Returns(testCase);
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(testCase));
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new Census.Models.CensusConfigModel
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
            var censusConfigServiceMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();


            censusConfigServiceMock.Setup(x => x.Get(It.IsAny<string>())).Returns(testCase);
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(testCase));
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Throws(new Exception());

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new Census.Models.CensusConfigModel
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
            var censusConfigServiceMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();


            censusConfigServiceMock.Setup(x => x.Get(It.IsAny<string>())).Returns(testCase);
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new Census.Models.CensusConfigModel
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
            var censusConfigServiceMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();

            censusConfigServiceMock.Setup(x => x.Get(It.IsAny<string>())).Returns(testCase);
            censusConfigServiceMock.Setup(x => x.AddAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Throws(new Exception());
            _schedulerFactoryMock.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>())).Returns(Task.FromResult<IScheduler>(null));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));
            censusSchedulingRepoMock.Setup(x => x.AddJobForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new CreateCensusConfigCommandHandler(logger, censusConfigServiceMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object);

            try
            {
                await handler.Handle(new CreateCensusConfigCommand
                {
                    CensusConfigEntity = new Census.Models.CensusConfigModel
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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();

            censusConfigMongoRepoMock.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));
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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();

            censusConfigMongoRepoMock.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Throws(new Exception());
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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();

            censusConfigMongoRepoMock.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<bool>(true));
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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();

            censusConfigMongoRepoMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(new CensusConfigEntity
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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();

            censusConfigMongoRepoMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult((CensusConfigEntity)null));

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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediatorMock = new Mock<IMediator>();

            censusConfigMongoRepoMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));
            censusConfigMongoRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new UpdateCensusCommandHandler(logger, censusConfigMongoRepoMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediatorMock.Object);

            try
            {
                var response = await handler.Handle(new UpdateCensusCommand
                {
                    Config = new Census.Models.CensusConfigModel
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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediatorMock = new Mock<IMediator>();

            censusConfigMongoRepoMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));
            censusConfigMongoRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Throws(new Exception());

            var handler = new UpdateCensusCommandHandler(logger, censusConfigMongoRepoMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediatorMock.Object);

            try
            {
                var response = await handler.Handle(new UpdateCensusCommand
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
            var censusConfigMongoRepoMock = new Mock<ICensusConfigMongoRepository>();
            var censusSchedulingRepoMock = new Mock<ICensusSchedulingRepository>();
            var mediatorMock = new Mock<IMediator>();

            censusConfigMongoRepoMock.Setup(x => x.Get(It.IsAny<string>())).Returns((CensusConfigEntity)null);
            censusConfigMongoRepoMock.Setup(x => x.UpdateAsync(It.IsAny<CensusConfigEntity>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult<CensusConfigEntity>(new CensusConfigEntity
            {
                FacilityID = facilityId,
                ScheduledTrigger = "0 0 0 * * ?"
            }));
            censusSchedulingRepoMock.Setup(x => x.UpdateJobsForFacility(It.IsAny<CensusConfigEntity>(), It.IsAny<CensusConfigEntity>(), It.IsAny<IScheduler>())).Returns(Task.FromResult<bool>(true));

            var handler = new UpdateCensusCommandHandler(logger, censusConfigMongoRepoMock.Object, _schedulerFactoryMock.Object, censusSchedulingRepoMock.Object, mediatorMock.Object);

            try
            {
                var response = await handler.Handle(new UpdateCensusCommand
                {
                    Config = new Census.Models.CensusConfigModel
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
