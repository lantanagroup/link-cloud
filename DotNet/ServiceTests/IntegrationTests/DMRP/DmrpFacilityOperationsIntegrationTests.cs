using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DMRP;

/// <summary>
/// Covers the wiring the unit tests cannot: that the module actually puts itself in front of the
/// host's facility operations, and that the schedule it derives comes from rows read out of the
/// host's database rather than from a stubbed source.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class DmrpFacilityOperationsIntegrationTests : IDisposable
{
    private const string FacilityId = "100";

    private readonly DmrpIntegrationTestFixture _fixture;
    private readonly IServiceScope _scope;
    private readonly IEntityRepository<MeasureMapping> _mappingRepository;
    private readonly IEntityRepository<FacilityReportingPlan> _planRepository;

    public DmrpFacilityOperationsIntegrationTests(DmrpIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _scope = fixture.ServiceProvider.CreateScope();

        _mappingRepository = _scope.ServiceProvider.GetRequiredService<IEntityRepository<MeasureMapping>>();
        _planRepository = _scope.ServiceProvider.GetRequiredService<IEntityRepository<FacilityReportingPlan>>();

        // The suite shares one database, so start each test from an empty table.
        foreach (var plan in _planRepository.GetAllAsync().GetAwaiter().GetResult())
        {
            _planRepository.Remove(plan);
        }

        _planRepository.SaveChangesAsync().GetAwaiter().GetResult();

        _fixture.FacilityOperationsMock.Reset();
    }

    public void Dispose()
    {
        _fixture.FacilityOperationsMock.Reset();
        _scope.Dispose();
    }

    private IFacilityOperations Operations => _scope.ServiceProvider.GetRequiredService<IFacilityOperations>();

    private async Task<MeasureMapping> AddMappingAsync(string dqm, Frequency frequency)
    {
        // Measure is required and (Measure, DQM) is unique, so each mapping needs its own name.
        var mapping = new MeasureMapping
        {
            Measure = $"MEASURE-{Guid.NewGuid():N}",
            DQM = dqm,
            Frequency = frequency
        };

        await _mappingRepository.AddAsync(mapping);
        await _mappingRepository.SaveChangesAsync();

        return mapping;
    }

    private async Task AddPlanAsync(MeasureMapping mapping, int month, int year)
    {
        await _planRepository.AddAsync(new FacilityReportingPlan
        {
            FacilityId = FacilityId,
            MeasureMappingId = mapping.Id,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = true
        });

        await _planRepository.SaveChangesAsync();
    }

    [Fact]
    public void The_module_puts_its_own_facility_operations_in_front_of_the_hosts()
    {
        Assert.IsType<DmrpFacilityOperations>(Operations);
    }

    [Fact]
    public async Task Create_schedules_the_dqms_the_facilitys_stored_plans_name()
    {
        var now = DateTime.UtcNow;

        var monthly = await AddMappingAsync("NHSNAcuteCareHospitalMonthlyInitialPopulation", Frequency.Monthly);
        var daily = await AddMappingAsync("NHSNAcuteCareHospitalDailyInitialPopulation", Frequency.Daily);

        await AddPlanAsync(monthly, now.Month, now.Year);
        await AddPlanAsync(daily, now.Month, now.Year);

        FacilityModel? reachedTheHost = null;

        _fixture.FacilityOperationsMock
            .Setup(o => o.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .Callback<FacilityModel, CancellationToken>((f, _) => reachedTheHost = f)
            .Returns(Task.CompletedTask);

        await Operations.CreateAsync(new FacilityModel
        {
            FacilityId = FacilityId,
            FacilityName = "Test Facility",
            TimeZone = "UTC"
        });

        Assert.NotNull(reachedTheHost);
        Assert.Equal(new[] { "NHSNAcuteCareHospitalMonthlyInitialPopulation" },
            reachedTheHost!.ScheduledReports.Monthly);
        Assert.Equal(new[] { "NHSNAcuteCareHospitalDailyInitialPopulation" },
            reachedTheHost.ScheduledReports.Daily);
        Assert.Empty(reachedTheHost.ScheduledReports.Weekly);
    }

    /// <summary>
    /// Plans stored for another period are history. Only the period the facility is currently in
    /// decides what it is scheduled for.
    /// </summary>
    [Fact]
    public async Task Create_ignores_plans_stored_for_a_different_period()
    {
        var now = DateTime.UtcNow;
        var otherPeriod = now.AddMonths(-1);

        var mapping = await AddMappingAsync("NHSNAcuteCareHospitalMonthlyInitialPopulation", Frequency.Monthly);
        await AddPlanAsync(mapping, otherPeriod.Month, otherPeriod.Year);

        FacilityModel? reachedTheHost = null;

        _fixture.FacilityOperationsMock
            .Setup(o => o.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .Callback<FacilityModel, CancellationToken>((f, _) => reachedTheHost = f)
            .Returns(Task.CompletedTask);

        await Operations.CreateAsync(new FacilityModel
        {
            FacilityId = FacilityId,
            FacilityName = "Test Facility",
            TimeZone = "UTC"
        });

        Assert.NotNull(reachedTheHost);
        Assert.Empty(reachedTheHost!.ScheduledReports.Monthly);
        Assert.Empty(reachedTheHost.ScheduledReports.Daily);
        Assert.Empty(reachedTheHost.ScheduledReports.Weekly);
    }

    [Fact]
    public async Task Delete_removes_the_facilitys_stored_reporting_plans()
    {
        var now = DateTime.UtcNow;

        var mapping = await AddMappingAsync("NHSNAcuteCareHospitalMonthlyInitialPopulation", Frequency.Monthly);
        await AddPlanAsync(mapping, now.Month, now.Year);

        await Operations.DeleteAsync(FacilityId);

        _fixture.FacilityOperationsMock.Verify(o => o.DeleteAsync(FacilityId, It.IsAny<CancellationToken>()),
            Times.Once);

        var remaining = await _planRepository.FindAsync(p => p.FacilityId == FacilityId);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task Soft_delete_keeps_the_facilitys_stored_reporting_plans()
    {
        var now = DateTime.UtcNow;

        var mapping = await AddMappingAsync("NHSNAcuteCareHospitalMonthlyInitialPopulation", Frequency.Monthly);
        await AddPlanAsync(mapping, now.Month, now.Year);

        await Operations.SoftDeleteAsync(FacilityId);

        _fixture.FacilityOperationsMock.Verify(o => o.SoftDeleteAsync(FacilityId, It.IsAny<CancellationToken>()),
            Times.Once);

        var remaining = await _planRepository.FindAsync(p => p.FacilityId == FacilityId);
        Assert.Single(remaining);
    }
}
