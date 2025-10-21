using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using QueryDispatch.Domain.Managers;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.QueryDispatch;

public class GetFacilityConfigurationTests
{
    private AutoMocker _mocker;

    [Fact]
    public async Task TestGetFacilityConfiguration()
    {
        _mocker = new AutoMocker();
        var _controller = _mocker.CreateInstance<QueryDispatchController>();

        _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.GetConfigEntity(It.IsAny<string>(), CancellationToken.None))
        .Returns(Task.FromResult(new QueryDispatchConfigurationEntity()));

        var result = await _controller.GetFacilityConfiguration(QueryDispatchTestsConstants.facilityId, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task NegativeTestGetFacilityConfiguration()
    {
        _mocker = new AutoMocker();
        var _controller = _mocker.CreateInstance<QueryDispatchController>();

        _mocker.GetMock<IQueryDispatchConfigurationManager>().Setup(x => x.GetConfigEntity(It.IsAny<string>(), CancellationToken.None))
       .ReturnsAsync((QueryDispatchConfigurationEntity)null);

        var result = await _controller.GetFacilityConfiguration("", CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
