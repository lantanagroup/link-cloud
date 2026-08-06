using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DMRP;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class MeasureMappingsControllerTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly MeasureMappingsController _controller;

    public MeasureMappingsControllerTests(DmrpIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var sp = _scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILogger<MeasureMappingsController>>();
        var manager = sp.GetRequiredService<IMeasureMappingManager>();
        var queries = sp.GetRequiredService<IMeasureMappingQueries>();

        _controller = new MeasureMappingsController(logger, manager, queries)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task CreateMeasureMapping_ThenGet_ReturnsCreatedRecord()
    {
        var createResult = await _controller.CreateMeasureMapping(new MeasureMappingModel(), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(createResult);
        var createdModel = Assert.IsType<MeasureMappingModel>(created.Value);
        Assert.False(string.IsNullOrEmpty(createdModel.Id));

        var getResult = await _controller.GetMeasureMapping(createdModel.Id!, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(getResult);
        var fetched = Assert.IsType<MeasureMappingModel>(ok.Value);
        Assert.Equal(createdModel.Id, fetched.Id);
    }

    [Fact]
    public async Task GetMeasureMapping_NotFound_Returns404()
    {
        var result = await _controller.GetMeasureMapping(Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateMeasureMapping_MismatchedId_ReturnsBadRequest()
    {
        var createResult = await _controller.CreateMeasureMapping(new MeasureMappingModel(), CancellationToken.None);
        var created = (MeasureMappingModel)((CreatedResult)createResult).Value!;

        var result = await _controller.UpdateMeasureMapping(created.Id!,
            new MeasureMappingModel { Id = Guid.NewGuid().ToString() }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteMeasureMapping_ThenGet_ReturnsNotFound()
    {
        var createResult = await _controller.CreateMeasureMapping(new MeasureMappingModel(), CancellationToken.None);
        var created = (MeasureMappingModel)((CreatedResult)createResult).Value!;

        var deleteResult = await _controller.DeleteMeasureMapping(created.Id!, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var getResult = await _controller.GetMeasureMapping(created.Id!, CancellationToken.None);
        Assert.IsType<NotFoundResult>(getResult);
    }

    [Fact]
    public async Task DeleteMeasureMapping_NotFound_Returns404()
    {
        var result = await _controller.DeleteMeasureMapping(Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
