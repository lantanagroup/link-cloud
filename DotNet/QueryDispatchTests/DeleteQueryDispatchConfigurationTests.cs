using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using Microsoft.AspNetCore.Http.HttpResults;
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
    public class DeleteQueryDispatchConfigurationTests
    {
        private AutoMocker _mocker;

      /*  [Fact]
        public async Task TestDeleteQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            _mocker.GetMock<IDeleteQueryDispatchConfigurationCommand>()
                .Setup(command => command.Execute(QueryDispatchTestsConstants.facilityId))
                .ReturnsAsync(true);

            var result = await _controller.DeleteQueryDispatchConfiguration(QueryDispatchTestsConstants.facilityId);
            Assert.IsType<ActionResult<RequestResponse>>(result);
        }

        [Fact]
        public async Task NegativeTestDeleteQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            var result = await _controller.DeleteQueryDispatchConfiguration("");
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }*/
    }
}
