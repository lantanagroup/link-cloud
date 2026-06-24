using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using System.Net;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class EncounterMappingControllerTests
{
    private const string FacilityId = "test-facility-id";
    private const string EncounterId = "test-encounter-id";
    private const string PatientId = "test-patient-id";

    // Real MVC services so CreateAsync's TryValidateModel/ValidationProblem run against the
    // actual DataAnnotations validator and ProblemDetailsFactory instead of throwing NRE.
    private static readonly IServiceProvider MvcServices = BuildMvcServices();

    private static IServiceProvider BuildMvcServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds a controller wired for the POST path: ControllerContext (for CreatedAtAction URL
    /// generation), ObjectValidator (for TryValidateModel) and ProblemDetailsFactory (for ValidationProblem).
    /// </summary>
    private static EncounterMappingController CreatePostController(AutoMocker mocker)
    {
        var controller = mocker.CreateInstance<EncounterMappingController>();
        controller.ControllerContext = new ControllerContext
        {
            ActionDescriptor = new ControllerActionDescriptor(),
            HttpContext = new DefaultHttpContext { RequestServices = MvcServices }
        };
        controller.ObjectValidator = MvcServices.GetRequiredService<IObjectModelValidator>();
        controller.ProblemDetailsFactory = MvcServices.GetRequiredService<ProblemDetailsFactory>();
        return controller;
    }

    #region GET /encounter-mappings/{id}

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsOk()
    {
        var mocker = new AutoMocker();
        var model = new EncounterMappingModel { EncounterMappingId = 1, FacilityId = FacilityId, EncounterId = EncounterId };
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByIdAsync(1))
            .ReturnsAsync(model);

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByIdAsync(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(model, ok.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((EncounterMappingModel?)null);

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByIdAsync(int.MaxValue);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByIdAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByIdAsync(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /encounter-mappings/facilities/{facilityId}

    [Fact]
    public async Task GetByFacilityIdAsync_ExistingFacility_ReturnsOkWithList()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new() { EncounterMappingId = 1, FacilityId = FacilityId, EncounterId = EncounterId }
            });

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAsync(FacilityId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<EncounterMappingModel>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAsync(string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_NonExistingFacility_ReturnsOkWithEmptyList()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<EncounterMappingModel>());

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAsync("NonExisting");

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<EncounterMappingModel>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAsync(FacilityId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /encounter-mappings/facilities/{facilityId}/encounters/{encounterId}

    [Fact]
    public async Task GetByFacilityIdAndEncounterIdAsync_ExistingRecord_ReturnsOk()
    {
        var mocker = new AutoMocker();
        var model = new EncounterMappingModel { EncounterMappingId = 1, FacilityId = FacilityId, EncounterId = EncounterId };
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAndEncounterIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(model);

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndEncounterIdAsync(FacilityId, EncounterId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(model, ok.Value);
    }

    [Fact]
    public async Task GetByFacilityIdAndEncounterIdAsync_NotFound_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAndEncounterIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((EncounterMappingModel?)null);

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndEncounterIdAsync(FacilityId, EncounterId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAndEncounterIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndEncounterIdAsync(string.Empty, EncounterId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAndEncounterIdAsync_EmptyEncounterId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndEncounterIdAsync(FacilityId, string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAndEncounterIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAndEncounterIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndEncounterIdAsync(FacilityId, EncounterId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /encounter-mappings/facilities/{facilityId}/patients/{patientId}

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_ExistingRecords_ReturnsOkWithList()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAndPatientIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncounterMappingModel>
            {
                new() { EncounterMappingId = 1, FacilityId = FacilityId, PatientId = PatientId }
            });

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndPatientIdAsync(FacilityId, PatientId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<EncounterMappingModel>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndPatientIdAsync(string.Empty, PatientId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_EmptyPatientId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndPatientIdAsync(FacilityId, string.Empty);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByFacilityIdAndPatientIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.GetByFacilityIdAndPatientIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.GetByFacilityIdAndPatientIdAsync(FacilityId, PatientId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region GET /encounter-mappings/facilities/{facilityId}/search

    [Fact]
    public async Task SearchAsync_WithResults_ReturnsOkWithPagedResults()
    {
        var mocker = new AutoMocker();
        var paged = new PagedConfigModel<EncounterMappingModel>(
            new List<EncounterMappingModel> { new() { EncounterMappingId = 1, FacilityId = FacilityId } },
            new PaginationMetadata(10, 1, 1));
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<EncounterMappingSearchModel>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(paged);

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.SearchAsync(FacilityId, new EncounterMappingSearchParameters());

        var ok = Assert.IsType<OkObjectResult>(result);
        var actual = Assert.IsAssignableFrom<PagedConfigModel<EncounterMappingModel>>(ok.Value);
        Assert.Single(actual.Records);
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsNoContent()
    {
        var mocker = new AutoMocker();
        var paged = new PagedConfigModel<EncounterMappingModel>(
            new List<EncounterMappingModel>(),
            new PaginationMetadata(10, 1, 0));
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<EncounterMappingSearchModel>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(paged);

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.SearchAsync(FacilityId, new EncounterMappingSearchParameters());

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task SearchAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.SearchAsync(string.Empty, new EncounterMappingSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<EncounterMappingSearchModel>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.SearchAsync(FacilityId, new EncounterMappingSearchParameters());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region PUT /encounter-mappings/{id}

    [Fact]
    public async Task UpdateByIdAsync_ExistingId_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateEncounterMappingModel>()))
            .ReturnsAsync(new EncounterMappingModel { EncounterMappingId = 1 });

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByIdAsync(1, new UpdateEncounterMappingApiModel { MappedToOrg = true });

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByIdAsync_NullModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByIdAsync(1, null!);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByIdAsync_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateEncounterMappingModel>()))
            .ThrowsAsync(new KeyNotFoundException("EncounterMapping with id 9999 not found."));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByIdAsync(int.MaxValue, new UpdateEncounterMappingApiModel { MappedToOrg = false });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.UpdateByIdAsync(It.IsAny<int>(), It.IsAny<UpdateEncounterMappingModel>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByIdAsync(1, new UpdateEncounterMappingApiModel { MappedToOrg = true });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region PUT /encounter-mappings/facilities/{facilityId}/encounters/{encounterId}

    [Fact]
    public async Task UpdateByFacilityIdAndEncounterIdAsync_ValidInput_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.UpdateByFacilityIdAndEncounterIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateEncounterMappingModel>()))
            .ReturnsAsync(new EncounterMappingModel { EncounterMappingId = 1 });

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByFacilityIdAndEncounterIdAsync(FacilityId, EncounterId, new UpdateEncounterMappingApiModel { MappedToOrg = true });

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateByFacilityIdAndEncounterIdAsync_EmptyFacilityId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByFacilityIdAndEncounterIdAsync(string.Empty, EncounterId, new UpdateEncounterMappingApiModel { MappedToOrg = true });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByFacilityIdAndEncounterIdAsync_EmptyEncounterId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByFacilityIdAndEncounterIdAsync(FacilityId, string.Empty, new UpdateEncounterMappingApiModel { MappedToOrg = true });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByFacilityIdAndEncounterIdAsync_NullModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByFacilityIdAndEncounterIdAsync(FacilityId, EncounterId, null!);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByFacilityIdAndEncounterIdAsync_NonExistingRecord_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.UpdateByFacilityIdAndEncounterIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateEncounterMappingModel>()))
            .ThrowsAsync(new KeyNotFoundException("EncounterMapping not found."));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByFacilityIdAndEncounterIdAsync(FacilityId, EncounterId, new UpdateEncounterMappingApiModel { MappedToOrg = false });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateByFacilityIdAndEncounterIdAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.UpdateByFacilityIdAndEncounterIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateEncounterMappingModel>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.UpdateByFacilityIdAndEncounterIdAsync(FacilityId, EncounterId, new UpdateEncounterMappingApiModel { MappedToOrg = true });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    #endregion

    #region POST /encounter-mappings

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsCreated()
    {
        var mocker = new AutoMocker();
        var created = new EncounterMappingModel { EncounterMappingId = 42, FacilityId = FacilityId, EncounterId = EncounterId, PatientId = PatientId };
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()))
            .ReturnsAsync(created);

        var controller = CreatePostController(mocker);

        var result = await controller.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = FacilityId,
            EncounterId = EncounterId,
            PatientId = PatientId
        });

        var createdAt = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(EncounterMappingController.GetByIdAsync), createdAt.ActionName);
        Assert.Equal(42, createdAt.RouteValues!["id"]);
        Assert.Same(created, createdAt.Value);
    }

    [Fact]
    public async Task CreateAsync_NullModel_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = mocker.CreateInstance<EncounterMappingController>();

        var result = await controller.CreateAsync(null!);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
    }

    [Theory]
    [InlineData("", EncounterId, PatientId)]   // FacilityId sanitizes to empty
    [InlineData(FacilityId, "", PatientId)]    // EncounterId sanitizes to empty
    [InlineData(FacilityId, EncounterId, "")]  // PatientId sanitizes to empty
    public async Task CreateAsync_RequiredFieldEmptyAfterSanitization_ReturnsValidationProblem(
        string facilityId, string encounterId, string patientId)
    {
        var mocker = new AutoMocker();
        var controller = CreatePostController(mocker);

        // "!!!" survives HTML sanitization but the [^a-zA-Z0-9-_. ] removal in SanitizeAndRemove
        // strips it to an empty string, so the [Required(AllowEmptyStrings = false)] re-validation should reject it.
        var result = await controller.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = string.IsNullOrEmpty(facilityId) ? "!!!" : facilityId,
            EncounterId = string.IsNullOrEmpty(encounterId) ? "!!!" : encounterId,
            PatientId = string.IsNullOrEmpty(patientId) ? "!!!" : patientId
        });

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);

        // The manager must never be invoked when post-sanitization validation fails.
        mocker.GetMock<IEncounterMappingManager>()
            .Verify(m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_SanitizesFieldsBeforeCreating()
    {
        var mocker = new AutoMocker();
        CreateEncounterMappingModel? captured = null;
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()))
            .Callback<CreateEncounterMappingModel>(m => captured = m)
            .ReturnsAsync(new EncounterMappingModel { EncounterMappingId = 1 });

        var controller = CreatePostController(mocker);

        var result = await controller.CreateAsync(new CreateEncounterMappingModel
        {
            // Trailing disallowed characters are stripped; the alphanumeric/-_. remainder survives.
            FacilityId = "facility-1!!!",
            EncounterId = "encounter-1@@@",
            PatientId = "patient-1###"
        });

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(captured);
        Assert.Equal("facility-1", captured!.FacilityId);
        Assert.Equal("encounter-1", captured.EncounterId);
        Assert.Equal("patient-1", captured.PatientId);
    }

    [Fact]
    public async Task CreateAsync_GenericException_ReturnsInternalServerError()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()))
            .ThrowsAsync(new Exception("boom"));

        var controller = CreatePostController(mocker);

        var result = await controller.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = FacilityId,
            EncounterId = EncounterId,
            PatientId = PatientId
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_MappingAlreadyExists_ReturnsConflict()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IEncounterMappingManager>()
            .Setup(m => m.CreateAsync(It.IsAny<CreateEncounterMappingModel>()))
            .ThrowsAsync(new EntityAlreadyExistsException());

        var controller = CreatePostController(mocker);

        var result = await controller.CreateAsync(new CreateEncounterMappingModel
        {
            FacilityId = FacilityId,
            EncounterId = EncounterId,
            PatientId = PatientId
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);

        Assert.Equal((int)HttpStatusCode.Conflict, objectResult.StatusCode);
        Assert.Equal("The request could not be completed because it conflicts with the current state of the resource.",
            problemDetails.Detail);
    }

    #endregion
}
