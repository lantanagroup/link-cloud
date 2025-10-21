using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class OrganizationLocationConfigurationControllerTests
{
    private const string FacilityId = "test-facility-id";

    #region GET /location-config/{id}

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsOk()
    {
        var mocker = new AutoMocker();
        var model = new OrganizationLocationConfigurationModel { ConfigId = 1, FacilityId = FacilityId };
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.GetByIdAsync(1))
            .ReturnsAsync(model);

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.GetByIdAsync(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(model, ok.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.GetByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Sequence contains no elements"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.GetByIdAsync(int.MaxValue);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Not Found", problem.Title);
    }

    [Fact]
    public async Task GetByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.GetByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.GetByIdAsync(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /location-config/facility/{facilityId}

    [Fact]
    public async Task GetByFacilityIdAsync_ExistingFacility_ReturnsOk()
    {
        var mocker = new AutoMocker();
        var list = new List<OrganizationLocationConfigurationModel>
        {
            new() { ConfigId = 1, FacilityId = FacilityId }
        };
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(list);

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.GetByFacilityIdAsync(FacilityId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var actual = Assert.IsAssignableFrom<List<OrganizationLocationConfigurationModel>>(ok.Value);
        Assert.Single(actual);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.GetByFacilityIdAsync(string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_NonExistingFacility_ReturnsOkWithEmptyList()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<OrganizationLocationConfigurationModel>());

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.GetByFacilityIdAsync("NonExisting");

        var ok = Assert.IsType<OkObjectResult>(result);
        var actual = Assert.IsAssignableFrom<List<OrganizationLocationConfigurationModel>>(ok.Value);
        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.GetByFacilityIdAsync(FacilityId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /location-config/facility/{facilityId}/search

    [Fact]
    public async Task SearchAsync_ValidFacility_ReturnsOkWithPagedResults()
    {
        var mocker = new AutoMocker();
        var paged = new PagedConfigModel<OrganizationLocationConfigurationModel>(
            new List<OrganizationLocationConfigurationModel> { new() { ConfigId = 1, FacilityId = FacilityId } },
            new PaginationMetadata(1, 10, 1));
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<OrganizationLocationConfigurationSearchModel>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(paged);

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.SearchAsync(FacilityId, new OrganizationLocationConfigurationSearchParameters());

        var ok = Assert.IsType<OkObjectResult>(result);
        var actual = Assert.IsAssignableFrom<PagedConfigModel<OrganizationLocationConfigurationModel>>(ok.Value);
        Assert.Single(actual.Records);
    }

    [Fact]
    public async Task SearchAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.SearchAsync(string.Empty, new OrganizationLocationConfigurationSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<OrganizationLocationConfigurationSearchModel>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.SearchAsync(FacilityId, new OrganizationLocationConfigurationSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region POST /location-config/facility/{facilityId}

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsCreatedAtRoute()
    {
        var mocker = new AutoMocker();
        var created = new OrganizationLocationConfigurationModel { ConfigId = 42, FacilityId = FacilityId };
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var apiModel = new CreateOrganizationLocationConfigurationApiModel
        {
            Description = "New Location Config",
            IsActive = true,
            Conditions = new List<CreateOrganizationLocationConditionModel>
            {
                new() { FhirPath = "Patient.location", Priority = 1 }
            }
        };

        var result = await controller.CreateAsync(FacilityId, apiModel);

        var createdResult = Assert.IsType<CreatedAtRouteResult>(result);
        Assert.Equal(nameof(OrganizationLocationConfigurationController.GetByIdAsync), createdResult.RouteName);
        Assert.Same(created, createdResult.Value);
    }

    [Fact]
    public async Task CreateAsync_NullModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.CreateAsync(FacilityId, null);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.CreateAsync(string.Empty, new CreateOrganizationLocationConfigurationApiModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateOrganizationLocationConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.CreateAsync(FacilityId, new CreateOrganizationLocationConfigurationApiModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region PUT /location-config/{id}

    [Fact]
    public async Task UpdateByIdAsync_ExistingId_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationLocationConfigurationModel { ConfigId = 1, FacilityId = FacilityId });

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.UpdateByIdAsync(1, new UpdateOrganizationLocationConfigurationModel { Description = "Updated" });

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByIdAsync_NullModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.UpdateByIdAsync(1, null!);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("OrganizationLocationConfiguration with id 9999 not found."));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.UpdateByIdAsync(int.MaxValue, new UpdateOrganizationLocationConfigurationModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateOrganizationLocationConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.UpdateByIdAsync(1, new UpdateOrganizationLocationConfigurationModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region PUT /location-config/facility/{facilityId}

    [Fact]
    public async Task UpdateByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.UpdateByFacilityIdAsync(It.IsAny<string>(), It.IsAny<UpdateOrganizationLocationConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrganizationLocationConfigurationModel> { new() { ConfigId = 1, FacilityId = FacilityId } });

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.UpdateByFacilityIdAsync(FacilityId, new UpdateOrganizationLocationConfigurationModel { IsActive = false });

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByFacilityIdAsync_NullModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.UpdateByFacilityIdAsync(FacilityId, null!);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.UpdateByFacilityIdAsync(string.Empty, new UpdateOrganizationLocationConfigurationModel());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    #endregion

    #region DELETE /location-config/{id}

    [Fact]
    public async Task DeleteByIdAsync_ExistingId_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.DeleteByIdAsync(1);

        Assert.IsType<AcceptedResult>(result);
        mocker.GetMock<IOrganizationLocationConfigurationManager>().Verify(m => m.DeleteByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.DeleteByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.DeleteByIdAsync(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region DELETE /location-config/facility/{facilityId}

    [Fact]
    public async Task DeleteByFacilityIdAsync_ExistingFacility_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.DeleteByFacilityIdAsync(FacilityId);

        Assert.IsType<AcceptedResult>(result);
        mocker.GetMock<IOrganizationLocationConfigurationManager>().Verify(m => m.DeleteByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.DeleteByFacilityIdAsync(string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteByFacilityIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IOrganizationLocationConfigurationManager>()
            .Setup(m => m.DeleteByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<OrganizationLocationConfigurationController>();

        var result = await controller.DeleteByFacilityIdAsync(FacilityId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion
}
