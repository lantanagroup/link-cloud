using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisitionUnitTests.Commands.Config.QueryPlanConfig
{
    public class SaveQueryPlanCommandTests
    {
        private AutoMocker _mocker;
        private const string facilityId = "testId";
        private const string queryPlan = "{\"key\":\"value\"}";

        [Fact]
        public async Task HandleTest()
        {
            _mocker = new AutoMocker();
            var handler = _mocker.CreateInstance<SaveQueryPlanCommandHandler>();
            var command = new SaveQueryPlanCommand
            {
                FacilityId = facilityId,
                QueryPlan = queryPlan,
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

            _mocker.GetMock<ITenantApiService>()
                .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
                QueryPlan = queryPlan,
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

            _mocker.GetMock<ITenantApiService>()
                .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
