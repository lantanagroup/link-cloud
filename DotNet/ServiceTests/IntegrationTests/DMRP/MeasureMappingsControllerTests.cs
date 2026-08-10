using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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
        var measureEvalClient = new Mock<IMeasureEvalServiceClient>();
        measureEvalClient
            .Setup(client => client.GetMeasureDefinitionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string dqm, CancellationToken _) => new LinkApiResponse<string>
            {
                StatusCode = dqm == "Unknown DQM" ? StatusCodes.Status404NotFound : StatusCodes.Status200OK
            });

        _controller = new MeasureMappingsController(logger, manager, queries, measureEvalClient.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task CreateMeasureMapping_ThenGet_ReturnsCreatedRecord()
    {
        var request = new MeasureMappingModel
        {
            Measure = "Initial Measure",
            DQM = "Initial DQM",
            Frequency = Frequency.Daily
        };

        var createResult = await _controller.CreateMeasureMapping(request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(createResult);
        var createdModel = Assert.IsType<MeasureMappingModel>(created.Value);
        Assert.False(string.IsNullOrEmpty(createdModel.Id));

        var getResult = await _controller.GetMeasureMapping(createdModel.Id!, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(getResult);
        var fetched = Assert.IsType<MeasureMappingModel>(ok.Value);
        Assert.Equal(createdModel.Id, fetched.Id);
        Assert.Equal(request.Measure, fetched.Measure);
        Assert.Equal(request.DQM, fetched.DQM);
        Assert.Equal(request.Frequency, fetched.Frequency);
    }

    [Fact]
    public async Task CreateMeasureMapping_WithUnknownDqm_ReturnsBadRequest()
    {
        var result = await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = "Initial Measure",
            DQM = "Unknown DQM",
            Frequency = Frequency.Daily
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("DQM 'Unknown DQM' was not found in MeasureEval.", badRequest.Value);
    }

    [Fact]
    public async Task CreateMeasureMapping_WithDuplicateMeasureAndDqm_ReturnsMeasureValidationProblem()
    {
        var measure = $"Duplicate Measure {Guid.NewGuid()}";
        const string dqm = "Duplicate DQM";

        await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = measure,
            DQM = dqm,
            Frequency = Frequency.Daily
        }, CancellationToken.None);

        var result = await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = measure,
            DQM = dqm,
            Frequency = Frequency.Daily
        }, CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal("A measure mapping for this measure and dQM already exists.", problemDetails.Errors["measure"].Single());
    }

    [Fact]
    public async Task UpdateMeasureMapping_ThenGet_PersistsRequestValues()
    {
        var createResult = await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = "Original Measure",
            DQM = "Original DQM",
            Frequency = Frequency.Daily
        }, CancellationToken.None);
        var created = Assert.IsType<MeasureMappingModel>(((CreatedResult)createResult).Value);

        var updateRequest = new MeasureMappingModel
        {
            Id = created.Id,
            Measure = "Updated Measure",
            DQM = "Updated DQM",
            Frequency = Frequency.Monthly
        };

        var updateResult = await _controller.UpdateMeasureMapping(created.Id!, updateRequest, CancellationToken.None);
        var accepted = Assert.IsType<AcceptedResult>(updateResult);
        var updated = Assert.IsType<MeasureMappingModel>(accepted.Value);
        Assert.Equal(updateRequest.Measure, updated.Measure);
        Assert.Equal(updateRequest.DQM, updated.DQM);
        Assert.Equal(updateRequest.Frequency, updated.Frequency);

        var getResult = await _controller.GetMeasureMapping(created.Id!, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(getResult);
        var fetched = Assert.IsType<MeasureMappingModel>(ok.Value);
        Assert.Equal(updateRequest.Measure, fetched.Measure);
        Assert.Equal(updateRequest.DQM, fetched.DQM);
        Assert.Equal(updateRequest.Frequency, fetched.Frequency);
    }

    [Fact]
    public async Task UpdateMeasureMapping_WithUnknownDqm_ReturnsBadRequestAndRetainsExistingValues()
    {
        var createResult = await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = "Original Measure",
            DQM = "Original DQM",
            Frequency = Frequency.Daily
        }, CancellationToken.None);
        var created = Assert.IsType<MeasureMappingModel>(((CreatedResult)createResult).Value);

        var updateResult = await _controller.UpdateMeasureMapping(created.Id!, new MeasureMappingModel
        {
            Id = created.Id,
            Measure = "Updated Measure",
            DQM = "Unknown DQM",
            Frequency = Frequency.Monthly
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(updateResult);
        Assert.Equal("DQM 'Unknown DQM' was not found in MeasureEval.", badRequest.Value);

        var getResult = await _controller.GetMeasureMapping(created.Id!, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(getResult);
        var persisted = Assert.IsType<MeasureMappingModel>(ok.Value);
        Assert.Equal("Original Measure", persisted.Measure);
        Assert.Equal("Original DQM", persisted.DQM);
        Assert.Equal(Frequency.Daily, persisted.Frequency);
    }

    [Fact]
    public async Task UpdateMeasureMapping_WithDuplicateMeasureAndDqm_ReturnsMeasureValidationProblem()
    {
        var original = Assert.IsType<MeasureMappingModel>(((CreatedResult)await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = $"Original Measure {Guid.NewGuid()}",
            DQM = $"Original DQM {Guid.NewGuid()}",
            Frequency = Frequency.Daily
        }, CancellationToken.None)).Value);
        var mappingToUpdate = Assert.IsType<MeasureMappingModel>(((CreatedResult)await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = $"Other Measure {Guid.NewGuid()}",
            DQM = $"Other DQM {Guid.NewGuid()}",
            Frequency = Frequency.Monthly
        }, CancellationToken.None)).Value);

        var result = await _controller.UpdateMeasureMapping(mappingToUpdate.Id!, new MeasureMappingModel
        {
            Id = mappingToUpdate.Id,
            Measure = original.Measure,
            DQM = original.DQM,
            Frequency = original.Frequency
        }, CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal("A measure mapping for this measure and dQM already exists.", problemDetails.Errors["measure"].Single());
    }

    [Fact]
    public async Task GetMeasureMappings_WithMeasureFilter_ReturnsMatchingRecord()
    {
        var measure = $"Search Measure {Guid.NewGuid()}";
        var created = Assert.IsType<MeasureMappingModel>(((CreatedResult)await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = measure,
            DQM = $"Search DQM {Guid.NewGuid()}",
            Frequency = Frequency.Daily
        }, CancellationToken.None)).Value);
        await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = $"Other Measure {Guid.NewGuid()}",
            DQM = $"Other DQM {Guid.NewGuid()}",
            Frequency = Frequency.Monthly
        }, CancellationToken.None);

        var result = await _controller.GetMeasureMappings(new SearchMeasureMappingDto
        {
            Measure = measure
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var pagedResult = Assert.IsType<PagedMeasureMappingDto>(ok.Value);
        var mapping = Assert.Single(pagedResult.Records);
        Assert.Equal(created.Id, mapping.Id);
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
    public async Task DeleteAllMeasureMappings_RemovesAllRecords()
    {
        await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = $"First Measure {Guid.NewGuid()}",
            DQM = $"First DQM {Guid.NewGuid()}",
            Frequency = Frequency.Daily
        }, CancellationToken.None);
        await _controller.CreateMeasureMapping(new MeasureMappingModel
        {
            Measure = $"Second Measure {Guid.NewGuid()}",
            DQM = $"Second DQM {Guid.NewGuid()}",
            Frequency = Frequency.Monthly
        }, CancellationToken.None);

        var deleteResult = await _controller.DeleteAllMeasureMappings(CancellationToken.None);

        Assert.IsType<NoContentResult>(deleteResult);

        var searchResult = await _controller.GetMeasureMappings(new SearchMeasureMappingDto(), CancellationToken.None);
        Assert.IsType<NoContentResult>(searchResult);
    }

    [Fact]
    public async Task DeleteMeasureMapping_NotFound_Returns404()
    {
        var result = await _controller.DeleteMeasureMapping(Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
