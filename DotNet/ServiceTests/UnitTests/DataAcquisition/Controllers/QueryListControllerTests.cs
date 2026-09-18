using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class QueryListControllerTests
{
    private static QueryListController CreateController(AutoMocker mocker)
    {
        // ApiSettings has no required dependencies for the negative-path tests.
        mocker.Use<IOptions<ApiSettings>>(Options.Create(new ApiSettings()));
        return mocker.CreateInstance<QueryListController>();
    }

    private static FhirListConfigurationModel CreateValidPostModel(string facilityId) => new()
    {
        FacilityId = facilityId,
        FhirBaseServerUrl = "http://example.com",
        EHRPatientLists = new List<EhrPatientListModel>
        {
            new() { FhirId = "test1", Status = ListType.Admit, TimeFrame = TimeFrame.MoreThan48Hours },
            new() { FhirId = "test2", Status = ListType.Discharge, TimeFrame = TimeFrame.MoreThan48Hours },
            new() { FhirId = "test3", Status = ListType.Admit, TimeFrame = TimeFrame.LessThan24Hours },
            new() { FhirId = "test4", Status = ListType.Discharge, TimeFrame = TimeFrame.LessThan24Hours },
            new() { FhirId = "test5", Status = ListType.Admit, TimeFrame = TimeFrame.Between24To48Hours },
            new() { FhirId = "test6", Status = ListType.Discharge, TimeFrame = TimeFrame.Between24To48Hours },
        }
    };

    // ==================== GetFhirConfiguration ====================

    [Fact]
    public async Task GetFhirConfiguration_ValidFacilityId_ReturnsOkWithConfiguration()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirQueryListConfigurationQueries>()
            .Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirListConfigurationModel { FacilityId = "Facility1", FhirBaseServerUrl = "http://example.com", EHRPatientLists = new() });

        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration("Facility1", includePatients: false, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<FhirListConfigurationModel>(okResult.Value);
    }

    [Fact]
    public async Task GetFhirConfiguration_InvalidFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration(string.Empty, includePatients: false, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetFhirConfiguration_NonExisting_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirQueryListConfigurationQueries>()
            .Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirListConfigurationModel?)null);

        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration("NonExisting", includePatients: false, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ==================== GetFhirConfiguration: includePatients ====================

    private static FhirListConfigurationModel ConfigWithTwoLists() => new()
    {
        FacilityId = "Facility1",
        FhirBaseServerUrl = "http://example.com",
        EHRPatientLists = new List<EhrPatientListModel>
        {
            new() { FhirId = "list-a", Status = ListType.Admit, TimeFrame = TimeFrame.LessThan24Hours },
            new() { FhirId = "list-b", Status = ListType.Discharge, TimeFrame = TimeFrame.LessThan24Hours },
        }
    };

    private static void SetupExistingConfig(AutoMocker mocker, FhirListConfigurationModel config)
    {
        mocker.GetMock<IFhirQueryListConfigurationQueries>()
            .Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
    }

    [Fact]
    public async Task GetFhirConfiguration_IncludePatientsFalse_DoesNotQueryEhr()
    {
        var mocker = new AutoMocker();
        SetupExistingConfig(mocker, ConfigWithTwoLists());
        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration("Facility1", includePatients: false, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        mocker.GetMock<IEhrPatientListService>()
            .Verify(s => s.PopulatePatientsAsync(It.IsAny<FhirListConfigurationModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFhirConfiguration_IncludePatientsTrue_ReturnsOkWithPatients()
    {
        var mocker = new AutoMocker();
        var config = ConfigWithTwoLists();
        SetupExistingConfig(mocker, config);
        mocker.GetMock<IEhrPatientListService>()
            .Setup(s => s.PopulatePatientsAsync(It.IsAny<FhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .Callback((FhirListConfigurationModel c, CancellationToken _) =>
            {
                c.EHRPatientLists[0].Patients = new List<EhrPatientListPatientModel>
                {
                    new() { Id = "PT-1", Name = "Doe, Jane" }
                };
                c.EHRPatientLists[1].Patients = new List<EhrPatientListPatientModel>();
            })
            .Returns(Task.CompletedTask);
        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration("Facility1", includePatients: true, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<FhirListConfigurationModel>(okResult.Value);
        var patient = Assert.Single(returned.EHRPatientLists[0].Patients!);
        Assert.Equal("PT-1", patient.Id);
        Assert.Equal("Doe, Jane", patient.Name);
        Assert.Empty(returned.EHRPatientLists[1].Patients!);
    }

    [Fact]
    public async Task GetFhirConfiguration_IncludePatientsTrue_NotFound_DoesNotQueryEhr()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirQueryListConfigurationQueries>()
            .Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirListConfigurationModel?)null);
        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration("NonExisting", includePatients: true, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        mocker.GetMock<IEhrPatientListService>()
            .Verify(s => s.PopulatePatientsAsync(It.IsAny<FhirListConfigurationModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFhirConfiguration_IncludePatientsTrue_MissingQueryConfiguration_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        SetupExistingConfig(mocker, ConfigWithTwoLists());
        mocker.GetMock<IEhrPatientListService>()
            .Setup(s => s.PopulatePatientsAsync(It.IsAny<FhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MissingFacilityConfigurationException("Missing FHIR query configuration."));
        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration("Facility1", includePatients: true, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task GetFhirConfiguration_IncludePatientsTrue_ListReadFails_ReturnsFailedDependency()
    {
        var mocker = new AutoMocker();
        SetupExistingConfig(mocker, ConfigWithTwoLists());
        mocker.GetMock<IEhrPatientListService>()
            .Setup(s => s.PopulatePatientsAsync(It.IsAny<FhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PatientListRetrievalFailedException("Unable to read list list-a from the EHR."));
        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration("Facility1", includePatients: true, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status424FailedDependency, problem.StatusCode);
    }

    [Fact]
    public async Task GetFhirConfiguration_IncludePatientsTrue_RateLimited_ReturnsServiceUnavailableWithRetryAfter()
    {
        var mocker = new AutoMocker();
        SetupExistingConfig(mocker, ConfigWithTwoLists());
        mocker.GetMock<IEhrPatientListService>()
            .Setup(s => s.PopulatePatientsAsync(It.IsAny<FhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TooManyRequestsException("slow down", TimeSpan.FromSeconds(30)));
        var controller = CreateController(mocker);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.GetFhirConfiguration("Facility1", includePatients: true, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("30", controller.Response.Headers["Retry-After"]);
    }

    [Fact]
    public async Task GetFhirConfiguration_IncludePatientsTrue_BlankFacilityId_ReturnsBadRequestWithoutQuerying()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.GetFhirConfiguration(" ", includePatients: true, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        mocker.GetMock<IEhrPatientListService>()
            .Verify(s => s.PopulatePatientsAsync(It.IsAny<FhirListConfigurationModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void EhrPatientListModel_NullPatients_IsOmittedFromJson()
    {
        // Existing clients must see a byte-identical payload when includePatients is not requested.
        var json = System.Text.Json.JsonSerializer.Serialize(ConfigWithTwoLists());

        Assert.DoesNotContain("patients", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EhrPatientListModel_EmptyPatients_IsSerializedAsEmptyArray()
    {
        // An empty list is a real answer and must stay distinguishable from "not requested".
        var config = ConfigWithTwoLists();
        config.EHRPatientLists[0].Patients = new List<EhrPatientListPatientModel>();

        var json = System.Text.Json.JsonSerializer.Serialize(config);

        Assert.Contains("\"Patients\":[]", json);
    }

    // ==================== PostFhirConfiguration ====================

    [Fact]
    public async Task PostFhirConfiguration_ValidModel_ReturnsOkWithConfiguration()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirListQueryConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateFhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirListConfigurationModel { FacilityId = "Facility1", FhirBaseServerUrl = "http://example.com", EHRPatientLists = new() });

        var controller = CreateController(mocker);

        var result = await controller.PostFhirConfiguration(CreateValidPostModel("Facility1"), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<FhirListConfigurationModel>(okResult.Value);
    }

    [Fact]
    public async Task PostFhirConfiguration_InvalidModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.PostFhirConfiguration(new FhirListConfigurationModel(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PostFhirConfiguration_DuplicateFhirIds_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);
        var config = new FhirListConfigurationModel
        {
            FacilityId = "Facility1",
            FhirBaseServerUrl = "http://example.com",
            EHRPatientLists = new List<EhrPatientListModel>
            {
                new() { FhirId = "test", Status = ListType.Admit, TimeFrame = TimeFrame.MoreThan48Hours },
                new() { FhirId = "test", Status = ListType.Discharge, TimeFrame = TimeFrame.MoreThan48Hours },
                new() { FhirId = "test", Status = ListType.Admit, TimeFrame = TimeFrame.LessThan24Hours },
                new() { FhirId = "test", Status = ListType.Discharge, TimeFrame = TimeFrame.LessThan24Hours },
                new() { FhirId = "test", Status = ListType.Admit, TimeFrame = TimeFrame.Between24To48Hours },
                new() { FhirId = "test", Status = ListType.Discharge, TimeFrame = TimeFrame.Between24To48Hours },
            }
        };

        var result = await controller.PostFhirConfiguration(config, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PostFhirConfiguration_Existing_ReturnsConflict()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirListQueryConfigurationManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateFhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityAlreadyExistsException("A FhirListConfiguration already exists for facilityId."));

        var controller = CreateController(mocker);

        var result = await controller.PostFhirConfiguration(CreateValidPostModel("Facility1"), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    // ==================== PutFhirConfiguration ====================

    [Fact]
    public async Task PutFhirConfiguration_ValidModel_ReturnsOkWithConfiguration()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirListQueryConfigurationManager>()
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateFhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirListConfigurationModel { FacilityId = "Facility1", FhirBaseServerUrl = "http://new.com", EHRPatientLists = new() });

        var controller = CreateController(mocker);

        var result = await controller.PutFhirConfiguration(CreateValidPostModel("Facility1"), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<FhirListConfigurationModel>(okResult.Value);
    }

    [Fact]
    public async Task PutFhirConfiguration_InvalidModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.PutFhirConfiguration(new FhirListConfigurationModel(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PutFhirConfiguration_NonExisting_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirListQueryConfigurationManager>()
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateFhirListConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MissingFacilityConfigurationException("No facility config."));

        var controller = CreateController(mocker);

        var result = await controller.PutFhirConfiguration(CreateValidPostModel("NonExisting"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ==================== DeleteFhirConfiguration ====================

    [Fact]
    public async Task DeleteFhirConfiguration_ValidFacilityId_ReturnsOk()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirQueryListConfigurationQueries>()
            .Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirListConfigurationModel { FacilityId = "Facility1", FhirBaseServerUrl = "http://example.com", EHRPatientLists = new() });
        mocker.GetMock<IFhirListQueryConfigurationManager>()
            .Setup(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController(mocker);

        var result = await controller.DeleteFhirConfiguration("Facility1", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeleteFhirConfiguration_InvalidFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.DeleteFhirConfiguration(string.Empty, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteFhirConfiguration_NonExisting_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFhirQueryListConfigurationQueries>()
            .Setup(x => x.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirListConfigurationModel?)null);

        var controller = CreateController(mocker);

        var result = await controller.DeleteFhirConfiguration("NonExisting", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
