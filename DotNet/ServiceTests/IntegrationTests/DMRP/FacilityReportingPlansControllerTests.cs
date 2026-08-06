using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Business.Queries;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
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

        _fixture.TenantApiServiceMock
            .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    public void Dispose() => _scope.Dispose();

    private async Task<string> CreateMappingAsync()
    {
        var mapping = new MeasureMapping();
        await _mappingRepository.AddAsync(mapping);
        await _mappingRepository.SaveChangesAsync();

        return mapping.Id;
    }

    private async Task<FacilityReportingPlanModel> ValidRequestAsync(string facilityId = FacilityId, int month = 5,
        int year = 2026, bool isReporting = true) => new()
        {
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
    public async Task CreateFacilityReportingPlan_DuplicatePeriod_ReturnsBadRequest()
    {
        var request = await ValidRequestAsync();

        await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);
        var second = await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(second);
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_UnknownMeasureMapping_ReturnsBadRequest()
    {
        var request = await ValidRequestAsync();
        request.MeasureMappingId = Guid.NewGuid().ToString();

        var result = await _controller.CreateFacilityReportingPlan(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_UnknownFacility_ReturnsBadRequest()
    {
        _fixture.TenantApiServiceMock
            .Setup(s => s.CheckFacilityExists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CreateFacilityReportingPlan(await ValidRequestAsync(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateFacilityReportingPlan_MonthOutOfRange_ReturnsBadRequest()
    {
        var result = await _controller.CreateFacilityReportingPlan(await ValidRequestAsync(month: 13), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetFacilityReportingPlan_NotFound_Returns404()
    {
        var result = await _controller.GetFacilityReportingPlan(Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
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
    public async Task GetFacilityReportingPlansForFacility_NoPlans_Returns204()
    {
        var result = await _controller.GetFacilityReportingPlansForFacility("no-such-facility", null, null, null, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetFacilityReportingPlansForFacility_MonthOutOfRange_ReturnsBadRequest()
    {
        var result = await _controller.GetFacilityReportingPlansForFacility(FacilityId, 0, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SearchFacilityReportingPlans_FiltersAndPages()
    {
        await CreatedPlanAsync(month: 5);
        await CreatedPlanAsync(month: 6);
        await CreatedPlanAsync(facilityId: OtherFacilityId, month: 5);

        var result = await _controller.SearchFacilityReportingPlans(FacilityId, null, null, null, null, null, null,
            pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        var paged = Assert.IsType<PagedFacilityReportingPlanDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, paged.Metadata.TotalCount);
        Assert.All(paged.Records, r => Assert.Equal(FacilityId, r.FacilityId));
    }

    [Fact]
    public async Task SearchFacilityReportingPlans_NoMatches_Returns204()
    {
        await CreatedPlanAsync();

        var result = await _controller.SearchFacilityReportingPlans("no-such-facility", null, null, null, null, null,
            null, pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task SearchFacilityReportingPlans_UnsortableColumn_ReturnsBadRequest()
    {
        var result = await _controller.SearchFacilityReportingPlans(null, null, null, null, null,
            sortBy: "DROP TABLE", sortOrder: null, pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_ChangesTheStoredPlan()
    {
        var created = await CreatedPlanAsync(month: 5, isReporting: true);

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!, new FacilityReportingPlanModel
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
    public async Task UpdateFacilityReportingPlan_MismatchedId_ReturnsBadRequest()
    {
        var created = await CreatedPlanAsync();

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!,
            new FacilityReportingPlanModel { Id = Guid.NewGuid().ToString() }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_UnknownId_Returns404()
    {
        var request = await ValidRequestAsync();

        var result = await _controller.UpdateFacilityReportingPlan(Guid.NewGuid().ToString(), request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateFacilityReportingPlan_InvalidPlan_ReturnsBadRequestNot404()
    {
        var created = await CreatedPlanAsync();

        var result = await _controller.UpdateFacilityReportingPlan(created.Id!, new FacilityReportingPlanModel
        {
            Id = created.Id,
            FacilityId = created.FacilityId,
            MeasureMappingId = created.MeasureMappingId,
            ReportingMonth = 99,
            ReportingYear = created.ReportingYear
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteFacilityReportingPlan_ThenGet_ReturnsNotFound()
    {
        var created = await CreatedPlanAsync();

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

    [Fact]
    public async Task DeleteFacilityReportingPlansForFacility_LeavesOtherFacilitiesAlone()
    {
        await CreatedPlanAsync();
        var survivor = await CreatedPlanAsync(facilityId: OtherFacilityId);

        var result = await _controller.DeleteFacilityReportingPlansForFacility(FacilityId, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        Assert.IsType<NoContentResult>(
            await _controller.GetFacilityReportingPlansForFacility(FacilityId, null, null, null, CancellationToken.None));
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

        var remaining = await _controller.SearchFacilityReportingPlans(null, null, null, null, null, null, null,
            pageSize: 10, pageNumber: 1, cancellationToken: CancellationToken.None);

        Assert.IsType<NoContentResult>(remaining);
    }
}
