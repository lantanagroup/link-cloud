using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.Queries;
using LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;

namespace QueryDispatchUnitTests
{
    public class UpdateQueryDispatchConfigurationTests
    {
        private AutoMocker _mocker;

        [Fact]
        public async Task TestUpdateQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();

            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            var validModel = new QueryDispatchConfiguration
            {
                FacilityId = QueryDispatchTestsConstants.facilityId,
                DispatchSchedules = new List<DispatchSchedule>()
            };

            _mocker.GetMock<ITenantApiService>()
                .Setup(x => x.CheckFacilityExists(QueryDispatchTestsConstants.facilityId, new CancellationToken()))
                .ReturnsAsync(true);

            _mocker.GetMock<IGetQueryDispatchConfigurationQuery>()
                .Setup(query => query.Execute(validModel.FacilityId))
                .ReturnsAsync(new QueryDispatchConfigurationEntity());

            _mocker.GetMock<IUpdateQueryDispatchConfigurationCommand>()
                .Setup(factory => factory.Execute(It.IsAny<QueryDispatchConfigurationEntity>(), validModel.DispatchSchedules))
                .Returns(Task.CompletedTask);

            var result = await _controller.UpdateQueryDispatchConfiguration(QueryDispatchTestsConstants.facilityId, validModel);
            Assert.IsType<NoContentResult>(result.Result);
        }

        [Fact]
        public async Task NegativeTestUpdateQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            var invalidModel = new QueryDispatchConfiguration
            {
                FacilityId = "",
                DispatchSchedules = new List<DispatchSchedule>()
            };

            var result = await _controller.UpdateQueryDispatchConfiguration(QueryDispatchTestsConstants.facilityId, invalidModel);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
