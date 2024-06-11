using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter;
using MediatR;
using Moq;
using Moq.AutoMock;

namespace DataAcquisitionUnitTests.Commands.Config.QueryPlanConfig
{
    public class SaveQueryPlanCommandTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";
        //private const string queryPlan = "{\"key\":\"value\"}";
        private static QueryPlanResult queryPlan = new QueryPlanResult { 
                QueryPlan = new QueryPlan
                {
                    Id = new Guid(),
                    PlanName = "testName",
                    FacilityId = "testFacilityId",
                    EHRDescription = "testEHRDescription",
                    LookBack = "PT01",
                    ReportType = "testReportType",
                    InitialQueries = new Dictionary<string, LantanaGroup.Link.DataAcquisition.Domain.Interfaces.IQueryConfig>
                    {
                        { "0", new LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig
                            {
                                ResourceType = "Patient",
                                Parameters = new List<IParameter>{ new LiteralParameter { Name = "testName", Literal = "testValue" } }
                            }
                        }
                    },
                    SupplementalQueries = new Dictionary<string, LantanaGroup.Link.DataAcquisition.Domain.Interfaces.IQueryConfig>
                    {
                        { "0", new LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig
                            {
                                ResourceType = "Patient",
                                Parameters = new List<IParameter>{ new LiteralParameter { Name = "testName", Literal = "testValue" } }
                            }
                        }
                        }
                    }
            };

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveQueryPlanCommandHandler>();
            var command = new SaveQueryPlanCommand
            {
                FacilityId = facilityId,
                QueryPlanResult = queryPlan,
                QueryPlanType = QueryPlanType.QueryPlans
            };

            _mocker.GetMock<IQueryPlanRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryPlan());

            _mocker.GetMock<IQueryPlanRepository>()
                .Setup(r => r.UpdateAsync(It.IsAny<QueryPlan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryPlan());

            _mocker.GetMock<IMediator>()
                .Setup(r => r.Send(It.IsAny<TriggerAuditEventCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Unit.Value);

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IQueryPlanRepository>()
                .Verify(r => r.UpdateAsync(It.IsAny<QueryPlan>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _mocker.GetMock<IMediator>()
                .Verify(r => r.Send(It.IsAny<TriggerAuditEventCommand>(), It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
        }

        [Fact]
        public async Task HandleTest_Without_QueryPlanType()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveQueryPlanCommandHandler>();
            var command = new SaveQueryPlanCommand
            {
                FacilityId = facilityId,
                QueryPlanResult = queryPlan,
                QueryPlanType = QueryPlanType.QueryPlans
            };

            _mocker.GetMock<IQueryPlanRepository>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((QueryPlan)null);

            _mocker.GetMock<IQueryPlanRepository>()
                .Setup(r => r.AddAsync(It.IsAny<QueryPlan>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Unit.Value));

            _mocker.GetMock<IMediator>()
                .Setup(r => r.Send(It.IsAny<TriggerAuditEventCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Unit.Value);

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IQueryPlanRepository>()
                .Verify(r => r.AddAsync(It.IsAny<QueryPlan>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _mocker.GetMock<IMediator>()
                .Verify(r => r.Send(It.IsAny<TriggerAuditEventCommand>(), It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(Unit.Value, result);
        }
    }
}
