using LantanaGroup.Link.QueryDispatch.Application.Models;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using QueryDispatch.Domain.Managers;
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

        [Fact]
        public async Task TestDeleteQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            /* _mocker.GetMock<IDeleteQueryDispatchConfigurationCommand>()
                 .Setup(command => command.Execute(QueryDispatchTestsConstants.facilityId))
                 .ReturnsAsync(true);*/

            _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.DeleteConfigEntity(It.IsAny<string>(), CancellationToken.None)).Returns(Task.FromResult(true));

            var result = await _controller.DeleteQueryDispatchConfiguration(QueryDispatchTestsConstants.facilityId, CancellationToken.None);
            Assert.IsType<ActionResult<RequestResponse>>(result);
        }

        [Fact]
        public async Task NegativeTestDeleteQueryDispatchConfigurationAsync()
        {
            _mocker = new AutoMocker();
            var _controller = _mocker.CreateInstance<QueryDispatchController>();

            var result = await _controller.DeleteQueryDispatchConfiguration("", CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
