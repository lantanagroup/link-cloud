using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter;
using MediatR;
using Moq;
using Moq.AutoMock;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace DataAcquisitionUnitTests.Commands.Config.QueryPlanConfig
{
    public class SaveQueryPlanCommandTests
    {
        private AutoMocker _mocker;

        private const string facilityId = "testId";

        //private const string queryPlan = "{\"key\":\"value\"}";
        private static QueryPlan queryPlan = new QueryPlan
        {
            Id = Guid.NewGuid().ToString(),
            PlanName = "testName",
            FacilityId = "testFacilityId",
            EHRDescription = "testEHRDescription",
            LookBack = "PT01",
            ReportType = "testReportType",
            InitialQueries = new Dictionary<string, LantanaGroup.Link.DataAcquisition.Domain.Interfaces.IQueryConfig>
            {
                {
                    "0", new LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig
                    {
                        ResourceType = "Patient",
                        Parameters = new List<IParameter>
                            { new LiteralParameter { Name = "testName", Literal = "testValue" } }
                    }
                }
            },
            SupplementalQueries =
                new Dictionary<string, LantanaGroup.Link.DataAcquisition.Domain.Interfaces.IQueryConfig>
                {
                    {
                        "0", new LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.ParameterQueryConfig
                        {
                            ResourceType = "Patient",
                            Parameters = new List<IParameter>
                                { new LiteralParameter { Name = "testName", Literal = "testValue" } }
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
                QueryPlan = queryPlan
            };

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueryPlan());

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.UpdateAsync(It.IsAny<QueryPlan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(command.QueryPlan);

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _mocker.GetMock<ITenantApiService>()
                .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var result = await handler.Handle(command, CancellationToken.None);

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Verify(r => r.UpdateAsync(It.IsAny<QueryPlan>(), It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(command.QueryPlan, result);
        }

        [Fact]
        public async Task HandleTest_Without_FacilityId()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveQueryPlanCommandHandler>();
            var command = new SaveQueryPlanCommand
            {
                FacilityId = string.Empty
            };

            var qp = _mocker.CreateInstance<QueryPlan>();

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((QueryPlan)null);

            _mocker.GetMock<IEntityRepository<QueryPlan>>()
                .Setup(r => r.AddAsync(It.IsAny<QueryPlan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(qp);

            _mocker.GetMock<IMediator>()
                .Setup(m => m.Send(It.IsAny<CheckIfTenantExistsQuery>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _mocker.GetMock<ITenantApiService>()
                .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
