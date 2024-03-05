using LantanaGroup.Link.QueryDispatch.Application.Queries;
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
    public class GetFacilityConfigurationTests
    {
        private AutoMocker _mocker;

        [Fact]
        public async void TestGetFacilityConfiguration()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            _mocker.GetMock<IGetQueryDispatchConfigurationQuery>()
                .Setup(query => query.Execute(It.IsAny<string>()))
                .ReturnsAsync(new QueryDispatchConfigurationEntity { });

            var result = await _controller.GetFacilityConfiguration(QueryDispatchTestsConstants.facilityId);
            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async void NegativeTestGetFacilityConfiguration()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            var result = await _controller.GetFacilityConfiguration("");
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
