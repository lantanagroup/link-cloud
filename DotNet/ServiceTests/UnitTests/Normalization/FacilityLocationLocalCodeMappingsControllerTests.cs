using LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;
using LantanaGroup.Link.Normalization.Controllers;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class FacilityLocationLocalCodeMappingsControllerTests
{
    [Fact]
    public async Task GetAll_PassesUnmappedAndPaginationToQuery()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        queries.Setup(query => query.Search(It.IsAny<FacilityLocationLocalCodeMappingSearchModel>()))
            .ReturnsAsync(CreatePage());
        var controller = CreateController(manager, queries);

        var result = await controller.GetAll(true, 25, 2);

        Assert.IsType<OkObjectResult>(result.Result);
        queries.Verify(query => query.Search(It.Is<FacilityLocationLocalCodeMappingSearchModel>(model =>
            model.Unmapped == true && model.PageSize == 25 && model.PageNumber == 2)), Times.Once);
    }

    [Fact]
    public async Task Get_MissingMapping_ReturnsNotFound()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        queries.Setup(query => query.Get("mapping-id")).ReturnsAsync((FacilityLocationLocalCodeMappingModel?)null);
        var controller = CreateController(manager, queries);

        var result = await controller.Get("mapping-id");

        AssertProblem(result.Result!, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetForFacility_PassesFacilityAndUnmappedFilterToQuery()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        queries.Setup(query => query.Search(It.IsAny<FacilityLocationLocalCodeMappingSearchModel>()))
            .ReturnsAsync(CreatePage());
        var controller = CreateController(manager, queries);

        var result = await controller.GetForFacility("facility-1", false, 10, 1);

        Assert.IsType<OkObjectResult>(result.Result);
        queries.Verify(query => query.Search(It.Is<FacilityLocationLocalCodeMappingSearchModel>(model =>
            model.FacilityId == "facility-1" && model.Unmapped == false)), Times.Once);
    }

    [Fact]
    public async Task GetForLocation_PassesFacilityLocationAndUnmappedFiltersToQuery()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        queries.Setup(query => query.Search(It.IsAny<FacilityLocationLocalCodeMappingSearchModel>()))
            .ReturnsAsync(CreatePage());
        var controller = CreateController(manager, queries);

        var result = await controller.GetForLocation("facility-1", "location-1", true, 10, 1);

        Assert.IsType<OkObjectResult>(result.Result);
        queries.Verify(query => query.Search(It.Is<FacilityLocationLocalCodeMappingSearchModel>(model =>
            model.FacilityId == "facility-1" && model.LocationId == "location-1" && model.Unmapped == true)), Times.Once);
    }

    [Fact]
    public async Task GetForLocation_BlankLocationId_ReturnsBadRequestWithoutQuerying()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.GetForLocation("facility-1", " ", null, 10, 1);

        AssertProblem(result.Result!, HttpStatusCode.BadRequest);
        queries.Verify(query => query.Search(It.IsAny<FacilityLocationLocalCodeMappingSearchModel>()), Times.Never);
    }

    [Fact]
    public async Task GetForLocalCode_PassesFacilityLocalCodeAndUnmappedFiltersToQuery()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        queries.Setup(query => query.Search(It.IsAny<FacilityLocationLocalCodeMappingSearchModel>()))
            .ReturnsAsync(CreatePage());
        var controller = CreateController(manager, queries);

        var result = await controller.GetForLocalCode("facility-1", "local-code", true, 10, 1);

        Assert.IsType<OkObjectResult>(result.Result);
        queries.Verify(query => query.Search(It.Is<FacilityLocationLocalCodeMappingSearchModel>(model =>
            model.FacilityId == "facility-1" && model.LocalCode == "local-code" && model.Unmapped == true)), Times.Once);
    }

    [Fact]
    public async Task Search_PassesOptionalMappingFiltersToQuery()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        queries.Setup(query => query.Search(It.IsAny<FacilityLocationLocalCodeMappingSearchModel>()))
            .ReturnsAsync(CreatePage());
        var controller = CreateController(manager, queries);
        var hslocId = Guid.NewGuid();

        var result = await controller.Search(new FacilityLocationLocalCodeMappingSearchModel
        {
            FacilityId = "facility-1",
            LocationId = "location-1",
            LocalCodeSystem = "urn:oid:1.2.3",
            LocalCode = "local-code",
            HSLOCId = hslocId,
            Unmapped = false,
            PageSize = 20,
            PageNumber = 3
        });

        Assert.IsType<OkObjectResult>(result.Result);
        queries.Verify(query => query.Search(It.Is<FacilityLocationLocalCodeMappingSearchModel>(model =>
            model.FacilityId == "facility-1" &&
            model.LocationId == "location-1" &&
            model.LocalCodeSystem == "urn:oid:1.2.3" &&
            model.LocalCode == "local-code" &&
            model.HSLOCId == hslocId &&
            model.Unmapped == false &&
            model.PageSize == 20 &&
            model.PageNumber == 3)), Times.Once);
    }

    [Fact]
    public async Task Post_ValidMapping_ReturnsCreatedAndPassesModelToManager()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        var mapping = CreateMapping();
        manager.Setup(service => service.Create("facility-1", It.IsAny<FacilityLocationLocalCodeMappingPostModel>()))
            .ReturnsAsync(mapping);
        var controller = CreateController(manager, queries);

        var result = await controller.Post("facility-1", new FacilityLocationLocalCodeMappingPostModel
        {
            LocationId = "location-1",
            LocalCodeSystem = "urn:oid:1.2.3",
            LocalCode = "local-code",
            HSLOCId = Guid.NewGuid()
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(FacilityLocationLocalCodeMappingsController.Get), created.ActionName);
        manager.Verify(service => service.Create("facility-1", It.Is<FacilityLocationLocalCodeMappingPostModel>(model =>
            model.LocationId == "location-1" && model.LocalCodeSystem == "urn:oid:1.2.3" && model.LocalCode == "local-code")), Times.Once);
    }

    [Fact]
    public async Task Post_MissingRequiredMappingField_ReturnsBadRequestWithoutCallingManager()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.Post("facility-1", new FacilityLocationLocalCodeMappingPostModel
        {
            LocationId = "location-1",
            LocalCodeSystem = "urn:oid:1.2.3"
        });

        AssertProblem(result.Result!, HttpStatusCode.BadRequest);
        manager.Verify(service => service.Create(It.IsAny<string>(), It.IsAny<FacilityLocationLocalCodeMappingPostModel>()), Times.Never);
    }

    [Fact]
    public async Task Put_ExistingMapping_ReturnsAccepted()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        manager.Setup(service => service.Update("mapping-id", It.IsAny<FacilityLocationLocalCodeMappingPutModel>()))
            .ReturnsAsync(CreateMapping());
        var controller = CreateController(manager, queries);

        var result = await controller.Put("mapping-id", new FacilityLocationLocalCodeMappingPutModel
        {
            LocalCodeSystem = "urn:oid:1.2.3",
            LocalCode = "local-code"
        });

        Assert.IsType<AcceptedAtActionResult>(result.Result);
        manager.Verify(service => service.Update("mapping-id", It.IsAny<FacilityLocationLocalCodeMappingPutModel>()), Times.Once);
    }

    [Fact]
    public async Task Put_MissingMapping_ReturnsNotFound()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        manager.Setup(service => service.Update("mapping-id", It.IsAny<FacilityLocationLocalCodeMappingPutModel>()))
            .ReturnsAsync((FacilityLocationLocalCodeMappingModel?)null);
        var controller = CreateController(manager, queries);

        var result = await controller.Put("mapping-id", new FacilityLocationLocalCodeMappingPutModel
        {
            LocalCodeSystem = "urn:oid:1.2.3",
            LocalCode = "local-code"
        });

        AssertProblem(result.Result!, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ValidMapping_DeletesAndReturnsNoContent()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.Delete("mapping-id");

        Assert.IsType<NoContentResult>(result);
        manager.Verify(service => service.Delete("mapping-id"), Times.Once);
    }

    [Fact]
    public async Task DeleteForFacility_ValidFacility_DeletesMappingsAndReturnsNoContent()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        var controller = CreateController(manager, queries);

        var result = await controller.DeleteForFacility("facility-1");

        Assert.IsType<NoContentResult>(result);
        manager.Verify(service => service.DeleteForFacility("facility-1"), Times.Once);
    }

    [Fact]
    public async Task Post_FacilityLocationMissing_ReturnsNotFoundProblemDetails()
    {
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        manager.Setup(service => service.Create(It.IsAny<string>(), It.IsAny<FacilityLocationLocalCodeMappingPostModel>()))
            .ThrowsAsync(new KeyNotFoundException("The requested facility location does not exist."));
        var controller = CreateController(manager, queries);

        var result = await controller.Post("facility-1", new FacilityLocationLocalCodeMappingPostModel
        {
            LocationId = "location-1",
            LocalCodeSystem = "urn:oid:1.2.3",
            LocalCode = "local-code"
        });

        var problem = AssertProblem(result.Result!, HttpStatusCode.NotFound);
        Assert.Equal("The requested facility location does not exist.", problem.Detail);
    }

    [Fact]
    public async Task Post_DuplicateMapping_ReturnsConflictProblemDetails()
    {
        const string errorMessage = "A mapping already exists for this facility location and local code.";
        var manager = new Mock<IFacilityLocationLocalCodeMappingManager>();
        var queries = new Mock<IFacilityLocationLocalCodeMappingQueries>();
        manager.Setup(service => service.Create(It.IsAny<string>(), It.IsAny<FacilityLocationLocalCodeMappingPostModel>()))
            .ThrowsAsync(new InvalidOperationException(errorMessage));
        var controller = CreateController(manager, queries);

        var result = await controller.Post("facility-1", new FacilityLocationLocalCodeMappingPostModel
        {
            LocationId = "location-1",
            LocalCodeSystem = "urn:oid:1.2.3",
            LocalCode = "local-code"
        });

        var problem = AssertProblem(result.Result!, HttpStatusCode.Conflict);
        Assert.Equal(errorMessage, problem.Detail);
    }

    private static FacilityLocationLocalCodeMappingsController CreateController(
        Mock<IFacilityLocationLocalCodeMappingManager> manager,
        Mock<IFacilityLocationLocalCodeMappingQueries> queries) => new(manager.Object, queries.Object);

    private static FacilityLocationLocalCodeMappingModel CreateMapping() => new()
    {
        Id = "mapping-id",
        FacilityId = "facility-1",
        LocationId = "location-1",
        LocalCodeSystem = "urn:oid:1.2.3",
        LocalCode = "local-code"
    };

    private static PagedConfigModel<FacilityLocationLocalCodeMappingModel> CreatePage() => new()
    {
        Records = [CreateMapping()],
        Metadata = new PaginationMetadata(10, 1, 1)
    };

    private static ProblemDetails AssertProblem(IActionResult result, HttpStatusCode expectedStatus)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal((int)expectedStatus, problem.Status);
        return problem;
    }
}