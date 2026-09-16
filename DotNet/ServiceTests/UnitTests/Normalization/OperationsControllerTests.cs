using LantanaGroup.Link.Normalization.Application.Models.Operations.HttpModels;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Controllers;
using LantanaGroup.Link.Normalization.Domain;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class OperationsControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveHSLOCMap_WithVendor_ReturnsProblemDetails(bool update)
    {
        var manager = new Mock<IOperationManager>(MockBehavior.Strict);
        var controller = new OperationsController(manager.Object, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var operation = new HSLOCMapOperation([]);
        List<Guid> vendorVersionIds = [Guid.NewGuid()];

        var result = update
            ? await controller.PutOperation(new PutOperationModel(Guid.NewGuid(), ["Location"], operation, false, null, vendorVersionIds))
            : await controller.PostOperation(new PostOperationModel(["Location"], operation, null, vendorVersionIds));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(400, problem.Status);
        Assert.Equal("HSLOC Map operations cannot be assigned to vendors.", problem.Detail);
        manager.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SaveAllowedOperation_ReachesManager(bool update, bool vendor)
    {
        var manager = new Mock<IOperationManager>();
        manager.Setup(candidate => candidate.CreateOperation(It.IsAny<CreateOperationModel>()))
            .ReturnsAsync(new TaskResult { IsSuccess = false, ErrorMessage = "Manager reached" });
        manager.Setup(candidate => candidate.UpdateOperation(It.IsAny<UpdateOperationModel>()))
            .ReturnsAsync(new TaskResult { IsSuccess = false, ErrorMessage = "Manager reached" });
        var tenantService = new Mock<ITenantApiService>();
        tenantService.Setup(candidate => candidate.CheckFacilityExists("facility", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = new OperationsController(manager.Object, null!, null!, tenantService.Object, null!, null!, null!, null!, null!, null!, null!);
        IOperation operation = vendor ? new CopyLocationOperation() : new HSLOCMapOperation([]);
        var facilityId = vendor ? null : "facility";
        List<Guid>? vendorVersionIds = vendor ? [Guid.NewGuid()] : null;

        var result = update
            ? await controller.PutOperation(new PutOperationModel(Guid.NewGuid(), ["Location"], operation, false, facilityId, vendorVersionIds))
            : await controller.PostOperation(new PostOperationModel(["Location"], operation, facilityId, vendorVersionIds));

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("Manager reached", problem.Detail);
    }

    [Fact]
    public async Task PostPreset_ForHSLOCMap_ReturnsBadRequestWithoutSaving()
    {
        var database = new Mock<IDatabase> { DefaultValue = DefaultValue.Mock };
        var resourceTypeId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        database.Setup(candidate => candidate.OperationResourceTypes.GetAsync(resourceTypeId))
            .ReturnsAsync(new OperationResourceType { OperationId = operationId });
        database.Setup(candidate => candidate.Operations.GetAsync(operationId))
            .ReturnsAsync(new Operation { OperationType = OperationType.HSLOCMap.ToString() });
        var manager = new VendorVersionOperationPresetManager(database.Object, null!, null!, null!);
        var controller = new VendorVersionOperationPresetsController(manager, null!);

        var result = await controller.Post(new VendorVersionOperationPresetPostModel
        {
            VendorVersionId = Guid.NewGuid(),
            OperationResourceTypeId = resourceTypeId
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("HSLOC Map operations cannot be assigned to vendors.", badRequest.Value);
        database.Verify(candidate => candidate.SaveChangesAsync(), Times.Never);
    }
}