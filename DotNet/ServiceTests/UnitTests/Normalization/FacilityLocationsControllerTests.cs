using LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;
using LantanaGroup.Link.Normalization.Controllers;
using LantanaGroup.Link.Normalization.Domain.Managers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class FacilityLocationsControllerTests
{
    [Fact]
    public async Task Post_ValidFacilityLocation_ReturnsCreatedAndPassesSanitizedModelToManager()
    {
        var manager = new Mock<IFacilityLocationManager>();
        manager.Setup(service => service.Create("facility-1", It.IsAny<FacilityLocationPostModel>()))
            .ReturnsAsync(CreateFacilityLocation());
        var controller = new FacilityLocationsController(manager.Object);

        var result = await controller.Post(" facility-1 ", new FacilityLocationPostModel
        {
            LocationId = " location-1 ",
            PartOfId = " parent-location ",
            LocationName = " Main location ",
            LocationAlias = " main "
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(FacilityLocationsController.Get), created.ActionName);
        manager.Verify(service => service.Create("facility-1", It.Is<FacilityLocationPostModel>(model =>
            model.LocationId == "location-1" &&
            model.PartOfId == "parent-location" &&
            model.LocationName == " Main location " &&
            model.LocationAlias == " main ")), Times.Once);
    }

    [Fact]
    public async Task Post_MissingLocationId_ReturnsBadRequestWithoutCallingManager()
    {
        var manager = new Mock<IFacilityLocationManager>();
        var controller = new FacilityLocationsController(manager.Object);

        var result = await controller.Post("facility-1", new FacilityLocationPostModel());

        AssertProblem(result.Result!, HttpStatusCode.BadRequest);
        manager.Verify(service => service.Create(It.IsAny<string>(), It.IsAny<FacilityLocationPostModel>()), Times.Never);
    }

    [Fact]
    public async Task Post_DuplicateFacilityLocation_ReturnsConflict()
    {
        var manager = new Mock<IFacilityLocationManager>();
        manager.Setup(service => service.Create("facility-1", It.IsAny<FacilityLocationPostModel>()))
            .ThrowsAsync(new InvalidOperationException("A facility location with the supplied location identifier already exists."));
        var controller = new FacilityLocationsController(manager.Object);

        var result = await controller.Post("facility-1", new FacilityLocationPostModel { LocationId = "location-1" });

        AssertProblem(result.Result!, HttpStatusCode.Conflict);
    }

    private static FacilityLocationModel CreateFacilityLocation() => new()
    {
        Id = "facility-location-id",
        FacilityId = "facility-1",
        LocationId = "location-1"
    };

    private static ProblemDetails AssertProblem(IActionResult result, HttpStatusCode expectedStatus)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)expectedStatus, objectResult.StatusCode);
        return Assert.IsType<ProblemDetails>(objectResult.Value);
    }
}