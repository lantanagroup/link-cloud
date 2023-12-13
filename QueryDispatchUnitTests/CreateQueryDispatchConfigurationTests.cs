using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Application.Queries;
using LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueryDispatchUnitTests
{
    public class CreateQueryDispatchConfigurationTests
    {
        private AutoMocker _mocker;

        [Fact]
        public async void TestCreateQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            var validModel = new QueryDispatchConfiguration
            {
                FacilityId = QueryDispatchTestsConstants.facilityId,
                DispatchSchedules = new List<DispatchSchedule>()
            };

            _mocker.GetMock<IGetQueryDispatchConfigurationQuery>()
                .Setup(query => query.Execute(It.IsAny<string>()))
                .ReturnsAsync((QueryDispatchConfigurationEntity)null);

            _mocker.GetMock<IQueryDispatchConfigurationFactory>()
                .Setup(factory => factory.CreateQueryDispatchConfiguration(It.IsAny<string>(), It.IsAny<List<DispatchSchedule>>()))
                .Returns(new QueryDispatchConfigurationEntity());

            _mocker.GetMock<ICreateQueryDispatchConfigurationCommand>()
                .Setup(command => command.Execute(It.IsAny<QueryDispatchConfigurationEntity>()));

            var result = await _controller.CreateQueryDispatchConfigurationAsync(validModel);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async void NegativeTestCreateQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            var invalidModel = new QueryDispatchConfiguration
            {
                FacilityId = "",
                DispatchSchedules = new List<DispatchSchedule>()
            };

            var result = await _controller.CreateQueryDispatchConfigurationAsync(invalidModel);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
