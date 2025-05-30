using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using QueryDispatch.Domain.Managers;

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
                .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.GetConfigEntity(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.FromResult(new QueryDispatchConfigurationEntity()));

            _mocker.GetMock<IQueryDispatchConfigurationFactory>()
               .Setup(factory => factory.CreateQueryDispatchConfiguration(It.IsAny<string>(), It.IsAny<List<DispatchSchedule>>()))
               .Returns(new QueryDispatchConfigurationEntity());

            _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.SaveConfigEntity(It.IsAny<QueryDispatchConfigurationEntity>(), validModel.DispatchSchedules, CancellationToken.None))
             .Returns(Task.FromResult(true));

            _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.AddConfigEntity(It.IsAny<QueryDispatchConfigurationEntity>(), CancellationToken.None))
              .Returns(Task.FromResult(true));


            var result = await _controller.UpdateQueryDispatchConfiguration(QueryDispatchTestsConstants.facilityId, validModel, CancellationToken.None);
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

            var result = await _controller.UpdateQueryDispatchConfiguration(QueryDispatchTestsConstants.facilityId, invalidModel, CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
