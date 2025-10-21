using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class OrganizationLocationMappingControllerTests
{
    private const string FacilityId = "test-facility-id";
    private const string LocationId = "test-location-id";

    #region GET /location-mappings/{id}

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsOk()
    {
        var mocker = new AutoMocker();
        var model = new OrganizationLocationMappingModel { LocationMappingId = 1, FacilityId = FacilityId, LocationId = LocationId };
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.GetByIdAsync(1))
            .ReturnsAsync(model);

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.GetByIdAsync(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(model, ok.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.GetByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Sequence contains no elements"));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.GetByIdAsync(int.MaxValue);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.GetByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.GetByIdAsync(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /location-mappings/facility/{facilityId}

    [Fact]
    public async Task GetByFacilityIdAsync_ExistingFacility_ReturnsOkWithList()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<OrganizationLocationMappingModel>
            {
                new() { LocationMappingId = 1, FacilityId = FacilityId, LocationId = LocationId }
            });

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.GetByFacilityIdAsync(FacilityId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<OrganizationLocationMappingModel>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.GetByFacilityIdAsync(string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_NonExistingFacility_ReturnsOkWithEmptyList()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<OrganizationLocationMappingModel>());

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.GetByFacilityIdAsync("NonExisting");

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<OrganizationLocationMappingModel>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.GetByFacilityIdAsync(FacilityId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /location-mappings/facility/{facilityId}/search

    [Fact]
    public async Task SearchAsync_ValidFacility_ReturnsOkWithPagedResults()
    {
        var mocker = new AutoMocker();
        var paged = new PagedConfigModel<OrganizationLocationMappingModel>(
            new List<OrganizationLocationMappingModel> { new() { LocationMappingId = 1, FacilityId = FacilityId, LocationId = LocationId } },
            new PaginationMetadata(1, 10, 1));
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<OrganizationLocationMappingSearchModel>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<SortOrder?>()))
            .ReturnsAsync(paged);

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.SearchAsync(FacilityId, new OrganizationLocationMappingSearchParameters());

        var ok = Assert.IsType<OkObjectResult>(result);
        var actual = Assert.IsAssignableFrom<PagedConfigModel<OrganizationLocationMappingModel>>(ok.Value);
        Assert.Single(actual.Records);
    }

    [Fact]
    public async Task SearchAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.SearchAsync(string.Empty, new OrganizationLocationMappingSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<OrganizationLocationMappingSearchModel>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<SortOrder?>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.SearchAsync(FacilityId, new OrganizationLocationMappingSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region PUT /location-mappings/{id}

    [Fact]
    public async Task UpdateByIdAsync_ExistingId_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()))
            .ReturnsAsync(new OrganizationLocationMappingModel { LocationMappingId = 1 });

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.UpdateByIdAsync(1, new UpdateOrganizationLocationMappingModel { LocationName = "Updated Name" });

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByIdAsync_NullModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.UpdateByIdAsync(1, null!);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()))
            .ThrowsAsync(new KeyNotFoundException("OrganizationLocationMapping with id 9999 not found."));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.UpdateByIdAsync(int.MaxValue, new UpdateOrganizationLocationMappingModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationMappingModel>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.UpdateByIdAsync(1, new UpdateOrganizationLocationMappingModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region DELETE /location-mappings/{id}

    [Fact]
    public async Task DeleteByIdAsync_ExistingId_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.DeleteByFacilityIdAsync(1);

        Assert.IsType<AcceptedResult>(result);
        mocker.GetMock<IOrganizationLocationMappingManager>().Verify(m => m.DeleteByIdAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingManager>()
            .Setup(m => m.DeleteByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.DeleteByFacilityIdAsync(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region DELETE /location-mappings/facility/{facilityId}

    [Fact]
    public async Task DeleteByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.DeleteByFacilityIdAsync(FacilityId);

        Assert.IsType<AcceptedResult>(result);
        mocker.GetMock<IOrganizationLocationMappingManager>().Verify(m => m.DeleteByFacilityIdAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.DeleteByFacilityIdAsync(string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationMappingManager>()
            .Setup(m => m.DeleteByFacilityIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationMappingController>();

        var result = await controller.DeleteByFacilityIdAsync(FacilityId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion
}
