using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DMRP;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class FacilityReportingPlansControllerTests : IDisposable
{
    private const string FacilityId = "100";
    private const string OtherFacilityId = "200";

    private readonly DmrpIntegrationTestFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly FacilityReportingPlansController _controller;
    private readonly IEntityRepository<MeasureMapping> _mappingRepository;
    private readonly IEntityRepository<FacilityReportingPlan> _planRepository;

    public FacilityReportingPlansControllerTests(DmrpIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _scope = fixture.ServiceProvider.CreateScope();
        var sp = _scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILogger<FacilityReportingPlansController>>();
        var manager = sp.GetRequiredService<IFacilityReportingPlanManager>();
        var queries = sp.GetRequiredService<IFacilityReportingPlanQueries>();

        _mappingRepository = sp.GetRequiredService<IEntityRepository<MeasureMapping>>();
        _planRepository = sp.GetRequiredService<IEntityRepository<FacilityReportingPlan>>();

        _controller = new FacilityReportingPlansController(logger, manager, queries)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // The suite shares one database, so start each test from an empty table.
        foreach (var plan in _planRepository.GetAllAsync().GetAwaiter().GetResult())
        {
            _planRepository.Remove(plan);
        }

        _planRepository.SaveChangesAsync().GetAwaiter().GetResult();

        _fixture.ResetFacilityExistence();
    }

    public void Dispose()
    {
        _fixture.ResetFacilityExistence();
        _scope.Dispose();
    }

    private async Task<string> CreateMappingAsync()
    {
        // Measure is required and (Measure, Dqm) is unique, so each mapping needs its own name.
        var mapping = new MeasureMapping
        {
            Measure = $"MEASURE-{Guid.NewGuid():N}",
            DQM = "NHSNAcuteCareHospitalMonthlyInitialPopulation",
            Frequency = Frequency.Monthly
        };

        await _mappingRepository.AddAsync(mapping);
        await _mappingRepository.SaveChangesAsync();

        return mapping.Id;
    }

    private async Task<FacilityReportingPlanRequest> ValidRequestAsync(string facilityId = FacilityId, int month = 5,
        int year = 2026, bool isReporting = true) => new()
        {
            FacilityId = facilityId,
            MeasureMappingId = await CreateMappingAsync(),
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting
        };

    private async Task<FacilityReportingPlanUpdateRequest> ValidUpdateRequestAsync(string? id = null,
        string facilityId = FacilityId, int month = 5, int year = 2026, bool isReporting = true) => new()
        {
            Id = id,
            FacilityId = facilityId,
            MeasureMappingId = await CreateMappingAsync(),
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting
        };

    private async Task<FacilityReportingPlanModel> CreatedPlanAsync(string facilityId = FacilityId, int month = 5,
        int year = 2026, bool isReporting = true)
    {
        var result = await _controller.CreateFacilityReportingPlan(
            await ValidRequestAsync(facilityId, month, year, isReporting), CancellationToken.None);

        return (FacilityReportingPlanModel)Assert.IsType<CreatedResult>(result).Value!;
    }

    private static ProblemDetails AssertProblem(IActionResult result, int status, string title)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, obj.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal(status, problem.Status);
        Assert.Equal(title, problem.Title);

        return problem;
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_ThenGet_RoundTripsEveryField()
    {
        var request = await ValidRequestAsync(month: 7, year: 2027, isReporting: false);

        var createResult = await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(createResult);
        var createdModel = Assert.IsType<FacilityReportingPlanModel>(created.Value);
        Assert.False(string.IsNullOrEmpty(createdModel.Id));
        Assert.Equal($"/api/dmrp/reporting-plans/{createdModel.Id}", created.Location);

        var getResult = await _controller.GetFacilityReportingPlan(createdModel.Id!, CancellationToken.None);

        var fetched = Assert.IsType<FacilityReportingPlanModel>(Assert.IsType<OkObjectResult>(getResult).Value);
        Assert.Equal(createdModel.Id, fetched.Id);
        Assert.Equal(request.FacilityId, fetched.FacilityId);
        Assert.Equal(request.MeasureMappingId, fetched.MeasureMappingId);
        Assert.Equal(7, fetched.ReportingMonth);
        Assert.Equal(2027, fetched.ReportingYear);
        Assert.False(fetched.IsReporting);
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_DuplicatePeriod_ReturnsConflictProblemDetails()
    {
        var request = await ValidRequestAsync();

        await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);
        var secondResult = await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);

        var problem = AssertProblem(secondResult, StatusCodes.Status409Conflict, "Conflict");
        Assert.Equal(
            $"A reporting plan already exists for facility {request.FacilityId}, measure mapping " +
            $"{request.MeasureMappingId} and period {request.ReportingMonth}/{request.ReportingYear}.",
            problem.Detail);
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_NullMeasureMappingId_ReturnsBadRequest()
    {
        var request = await ValidRequestAsync();
        request.MeasureMappingId = null;

        var result = await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
        Assert.Equal("MeasureMappingId is required.", problem.Detail);
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_UnknownMeasureMapping_ReturnsBadRequest()
    {
        var request = await ValidRequestAsync();
        request.MeasureMappingId = Guid.NewGuid().ToString();

        var result = await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_UnknownFacility_ReturnsBadRequest()
    {
        _fixture.FacilityExistenceMock
            .Setup(s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CreateFacilityReportingPlan(await ValidRequestAsync(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_MonthOutOfRange_ReturnsBadRequest()
    {
        var result = await _controller.CreateFacilityReportingPlan(await ValidRequestAsync(month: 13), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Fact]
    public async Task GetFacilityReportingPlan_NotFound_Returns404()
    {
        var result = await _controller.GetFacilityReportingPlan(Guid.NewGuid().ToString(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "Not Found");
    }

    [Fact]
    public async Task GetFacilityReportingPlansForFacility_ReturnsThatFacilitysPlansOnly()
    {
        await CreatedPlanAsync();
        await CreatedPlanAsync(facilityId: OtherFacilityId);

        var result = await _controller.GetFacilityReportingPlansForFacility(FacilityId, null, null, null, CancellationToken.None);

        var plans = Assert.IsType<List<FacilityReportingPlanModel>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(plans);
        Assert.Equal(FacilityId, plans[0].FacilityId);
    }

    [Fact]
    public async Task GetFacilityReportingPlansForFacility_FiltersByPeriodAndReportingState()
    {
        await CreatedPlanAsync(month: 5, year: 2026, isReporting: true);
        await CreatedPlanAsync(month: 6, year: 2026, isReporting: false);

        var may = await _controller.GetFacilityReportingPlansForFacility(FacilityId, 5, 2026, null, CancellationToken.None);
        var notReporting = await _controller.GetFacilityReportingPlansForFacility(FacilityId, null, null, false, CancellationToken.None);

        Assert.Single(Assert.IsType<List<FacilityReportingPlanModel>>(Assert.IsType<OkObjectResult>(may).Value));

        var unenrolled = Assert.IsType<List<FacilityReportingPlanModel>>(Assert.IsType<OkObjectResult>(notReporting).Value);
        Assert.Single(unenrolled);
        Assert.Equal(6, unenrolled[0].ReportingMonth);
    }

    [Fact]
    public async Task GetFacilityReportingPlansForFacility_NoPlans_ReturnsEmptyList()
    {
        var result = await _controller.GetFacilityReportingPlansForFacility("no-such-facility", null, null, null, CancellationToken.None);

        Assert.Empty(Assert.IsType<List<FacilityReportingPlanModel>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [Fact]
    public async Task GetFacilityReportingPlansForFacility_MonthOutOfRange_ReturnsBadRequest()
    {
        var result = await _controller.GetFacilityReportingPlansForFacility(FacilityId, 0, null, null, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Fact]
    public async Task SearchFacilityReportingPlans_FiltersAndPages()
    {
        await CreatedPlanAsync(month: 5);
        await CreatedPlanAsync(month: 6);
        await CreatedPlanAsync(facilityId: OtherFacilityId, month: 5);

        var result = await _controller.SearchFacilityReportingPlans(
            new FacilityReportingPlanSearchFilters { FacilityId = FacilityId },
            sortBy: null, sortOrder: null, pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        var paged = Assert.IsType<PagedFacilityReportingPlanDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, paged.Metadata.TotalCount);
        Assert.All(paged.Records, r => Assert.Equal(FacilityId, r.FacilityId));
    }

    [Fact]
    public async Task SearchFacilityReportingPlans_NoMatches_ReturnsEmptyPageWithMetadata()
    {
        await CreatedPlanAsync();

        var result = await _controller.SearchFacilityReportingPlans(
            new FacilityReportingPlanSearchFilters { FacilityId = "no-such-facility" },
            sortBy: null, sortOrder: null, pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        var paged = Assert.IsType<PagedFacilityReportingPlanDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Empty(paged.Records);
        Assert.Equal(0, paged.Metadata.TotalCount);
        Assert.Equal(1, paged.Metadata.PageNumber);
    }

    [Fact]
    public async Task SearchFacilityReportingPlans_UnsortableColumn_ReturnsBadRequest()
    {
        var result = await _controller.SearchFacilityReportingPlans(new FacilityReportingPlanSearchFilters(),
            sortBy: "DROP TABLE", sortOrder: null, pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("")]
    public async Task SearchFacilityReportingPlans_SortByThatIsBlankAfterSanitizing_FallsBackToTheDefault(string sortBy)
    {
        await CreatedPlanAsync();

        var result = await _controller.SearchFacilityReportingPlans(new FacilityReportingPlanSearchFilters(),
            sortBy: sortBy, sortOrder: null, pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_ChangesTheStoredPlan()
    {
        var created = await CreatedPlanAsync(month: 5, isReporting: true);

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!, new FacilityReportingPlanUpdateRequest
        {
            Id = created.Id,
            FacilityId = created.FacilityId,
            MeasureMappingId = created.MeasureMappingId,
            ReportingMonth = 8,
            ReportingYear = created.ReportingYear,
            IsReporting = false
        }, CancellationToken.None);

        var updated = Assert.IsType<FacilityReportingPlanModel>(Assert.IsType<AcceptedResult>(result).Value);
        Assert.Equal(8, updated.ReportingMonth);
        Assert.False(updated.IsReporting);
        Assert.NotNull(updated.ModifyDate);
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_MovedOntoAnotherPlansPeriod_ReturnsConflictProblemDetails()
    {
        var first = await CreatedPlanAsync(month: 5);
        var second = await CreatedPlanAsync(month: 6);

        var result = await _controller.UpdateFacilityReportingPlan(second.Id!, new FacilityReportingPlanUpdateRequest
        {
            Id = second.Id,
            FacilityId = first.FacilityId,
            MeasureMappingId = first.MeasureMappingId,
            ReportingMonth = first.ReportingMonth,
            ReportingYear = first.ReportingYear,
            IsReporting = second.IsReporting
        }, CancellationToken.None);

        var problem = AssertProblem(result, StatusCodes.Status409Conflict, "Conflict");
        Assert.Equal(
            $"A reporting plan already exists for facility {first.FacilityId}, measure mapping " +
            $"{first.MeasureMappingId} and period {first.ReportingMonth}/{first.ReportingYear}.",
            problem.Detail);
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_MismatchedId_ReturnsBadRequest()
    {
        var created = await CreatedPlanAsync();

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!,
            new FacilityReportingPlanUpdateRequest { Id = Guid.NewGuid().ToString() }, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_UnknownId_Returns404()
    {
        var unknownId = Guid.NewGuid().ToString();
        var request = await ValidUpdateRequestAsync(id: unknownId);

        var result = await _controller.UpdateFacilityReportingPlan(unknownId, request, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "Not Found");
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_MissingId_ReturnsBadRequest()
    {
        var created = await CreatedPlanAsync();

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!,
            await ValidUpdateRequestAsync(id: null), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_InvalidPlan_ReturnsBadRequestNot404()
    {
        var created = await CreatedPlanAsync();

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!, new FacilityReportingPlanUpdateRequest
        {
            Id = created.Id,
            FacilityId = created.FacilityId,
            MeasureMappingId = created.MeasureMappingId,
            ReportingMonth = 99,
            ReportingYear = created.ReportingYear
        }, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Bad Request");
    }

    [Fact]
    public async Task DeleteFacilityReportingPlan_ThenGet_ReturnsNotFound()
    {
        var created = await CreatedPlanAsync();

        var deleteResult = await _controller.DeleteFacilityReportingPlan(created.Id!, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var getResult = await _controller.GetFacilityReportingPlan(created.Id!, CancellationToken.None);
        AssertProblem(getResult, StatusCodes.Status404NotFound, "Not Found");
    }

    [Fact]
    public async Task DeleteFacilityReportingPlan_NotFound_Returns404()
    {
        var result = await _controller.DeleteFacilityReportingPlan(Guid.NewGuid().ToString(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, "Not Found");
    }

    [Fact]
    public async Task DeleteFacilityReportingPlansForFacility_LeavesOtherFacilitiesAlone()
    {
        await CreatedPlanAsync();
        var survivor = await CreatedPlanAsync(facilityId: OtherFacilityId);

        var result = await _controller.DeleteFacilityReportingPlansForFacility(FacilityId, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        var cleared = await _controller.GetFacilityReportingPlansForFacility(FacilityId, null, null, null, CancellationToken.None);
        Assert.Empty(Assert.IsType<List<FacilityReportingPlanModel>>(Assert.IsType<OkObjectResult>(cleared).Value));
        Assert.IsType<OkObjectResult>(
            await _controller.GetFacilityReportingPlan(survivor.Id!, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteFacilityReportingPlansForFacility_FacilityWithNoPlans_Returns204()
    {
        var result = await _controller.DeleteFacilityReportingPlansForFacility("no-such-facility", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteFacilityReportingPlans_EmptiesTheTable()
    {
        await CreatedPlanAsync();
        await CreatedPlanAsync(facilityId: OtherFacilityId);

        var result = await _controller.DeleteFacilityReportingPlans(CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        var remaining = await _controller.SearchFacilityReportingPlans(new FacilityReportingPlanSearchFilters(),
            sortBy: null, sortOrder: null, pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        Assert.Empty(Assert.IsType<PagedFacilityReportingPlanDto>(Assert.IsType<OkObjectResult>(remaining).Value).Records);
    }
}
