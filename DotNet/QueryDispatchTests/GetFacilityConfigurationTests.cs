
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using QueryDispatch.Domain.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace QueryDispatchUnitTests
{
    public class GetFacilityConfigurationTests
    {
        private AutoMocker _mocker;

        [Fact]
        public async void TestGetFacilityConfiguration()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.GetConfigEntity(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.FromResult(new QueryDispatchConfigurationEntity()));

            var result = await _controller.GetFacilityConfiguration(QueryDispatchTestsConstants.facilityId, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async void NegativeTestGetFacilityConfiguration()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.GetConfigEntity(It.IsAny<string>(), CancellationToken.None))
           .ReturnsAsync((QueryDispatchConfigurationEntity)null);

            var result = await _controller.GetFacilityConfiguration("", CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
