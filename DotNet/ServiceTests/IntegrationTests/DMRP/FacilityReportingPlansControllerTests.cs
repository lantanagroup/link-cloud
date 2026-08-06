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
public class FacilityReportingPlansControllerTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly FacilityReportingPlansController _controller;

    public FacilityReportingPlansControllerTests(DmrpIntegrationTestFixture fixture)
    {
        _scope = fixture.ServiceProvider.CreateScope();
        var sp = _scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILogger<FacilityReportingPlansController>>();
        var manager = sp.GetRequiredService<IFacilityReportingPlanManager>();
        var queries = sp.GetRequiredService<IFacilityReportingPlanQueries>();

        _controller = new FacilityReportingPlansController(logger, manager, queries)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task CreateFacilityReportingPlan_ThenGet_ReturnsCreatedRecord()
    {
        var createResult = await _controller.CreateFacilityReportingPlan(new FacilityReportingPlanModel(), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(createResult);
        var createdModel = Assert.IsType<FacilityReportingPlanModel>(created.Value);
        Assert.False(string.IsNullOrEmpty(createdModel.Id));

        var getResult = await _controller.GetFacilityReportingPlan(createdModel.Id!, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(getResult);
        var fetched = Assert.IsType<FacilityReportingPlanModel>(ok.Value);
        Assert.Equal(createdModel.Id, fetched.Id);
    }

    [Fact]
    public async Task GetFacilityReportingPlan_NotFound_Returns404()
    {
        var result = await _controller.GetFacilityReportingPlan(Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_MismatchedId_ReturnsBadRequest()
    {
        var createResult = await _controller.CreateFacilityReportingPlan(new FacilityReportingPlanModel(), CancellationToken.None);
        var created = (FacilityReportingPlanModel)((CreatedResult)createResult).Value!;

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!,
            new FacilityReportingPlanModel { Id = Guid.NewGuid().ToString() }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteFacilityReportingPlan_ThenGet_ReturnsNotFound()
    {
        var createResult = await _controller.CreateFacilityReportingPlan(new FacilityReportingPlanModel(), CancellationToken.None);
        var created = (FacilityReportingPlanModel)((CreatedResult)createResult).Value!;

        var deleteResult = await _controller.DeleteFacilityReportingPlan(created.Id!, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var getResult = await _controller.GetFacilityReportingPlan(created.Id!, CancellationToken.None);
        Assert.IsType<NotFoundResult>(getResult);
    }

    [Fact]
    public async Task DeleteFacilityReportingPlan_NotFound_Returns404()
    {
        var result = await _controller.DeleteFacilityReportingPlan(Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
