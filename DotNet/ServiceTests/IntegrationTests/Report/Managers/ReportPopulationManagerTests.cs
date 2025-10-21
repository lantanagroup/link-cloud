using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportPopulationManagerTests
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportPopulationManagerTests(ReportIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    #region Argument Validation Tests

    [Fact]
    public async Task AddRangeAsync_WithNullModels_DoesNothing()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var countBefore = await context.ReportPopulation.CountAsync();

        await sut.AddRangeAsync(null!, default);

        var countAfter = await context.ReportPopulation.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task AddRangeAsync_WithEmptyModels_DoesNothing()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var countBefore = await context.ReportPopulation.CountAsync();

        await sut.AddRangeAsync(Array.Empty<ReportPopulationModel>(), default);

        var countAfter = await context.ReportPopulation.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task UpdateAsync_WithNonexistentId_Throws()
    {
        using var scope = _scopeFactory.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
        var model = new ReportPopulationModel { Id = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(model, default));
    }

    #endregion

    #region Add & Find Tests

    [Fact]
    public async Task AddAsync_AddsPopulation()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = "fac-1";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreatePopulationModel(facilityId: facilityId, reportScheduleId: scheduleId);

        await sut.AddAsync(model, default);

        var entity = await context.ReportPopulation.FirstOrDefaultAsync(x => x.Id == model.Id);
        Assert.NotNull(entity);
        Assert.Equal(facilityId, entity.FacilityId);
    }

    [Fact]
    public async Task AddRangeAsync_AddsMultiplePopulations()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = "fac-multi";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var models = new[]
        {
            CreatePopulationModel(facilityId: facilityId, reportScheduleId: scheduleId),
            CreatePopulationModel(facilityId: facilityId, reportScheduleId: scheduleId)
        };

        var countBefore = await context.ReportPopulation.CountAsync();

        await sut.AddRangeAsync(models, default);

        Assert.Equal(countBefore + 2, await context.ReportPopulation.CountAsync());
    }

    [Fact]
    public async Task FindAsync_ReturnsMatchingPopulations()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
        var model = CreatePopulationModel();
        await SeedAsync(context, model);

        var result = await sut.FindAsync(x => x.FacilityId == model.FacilityId, default);
        Assert.Single(result);
        Assert.Equal(model.FacilityId, result[0].FacilityId);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_ReturnsSingleOrNull()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
        var model = CreatePopulationModel();
        await SeedAsync(context, model);

        var found = await sut.SingleOrDefaultAsync(x => x.Id == model.Id, default);
        Assert.NotNull(found);
        Assert.Equal(model.Id, found!.Id);

        var nonExistentId = Guid.NewGuid();
        var notFound = await sut.SingleOrDefaultAsync(x => x.Id == nonExistentId, default);
        Assert.Null(notFound);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_UpdatesPopulation()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
        var model = CreatePopulationModel();
        await SeedAsync(context, model);

        model.Measure = "updated-measure";
        var updated = await sut.UpdateAsync(model, default);
        Assert.Equal("updated-measure", updated.Measure);
        var entity = context.ReportPopulation.First(x => x.Id == model.Id);
        Assert.Equal("updated-measure", entity.Measure);
    }

    [Fact]
    public async Task UpdateAsyncWithAggregateResult_UpdatesAndAggregates()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
        var model = CreatePopulationModel();
        await SeedAsync(context, model);

        var agg = new AggregateMeasureReportResult
        {
            Measure = "agg-measure",
            ReportType = model.ReportType,
            MeasureReportId = Guid.NewGuid().ToString(),
            PopulationList = new List<AggregateMeasureReportPopulation>
            {
                new AggregateMeasureReportPopulation { PopulationId = "pop1", PopulationCode = new Hl7.Fhir.Model.CodeableConcept("system","c1"), PopulationCount = 5 }
            }
        };

        var updated = await sut.UpdateAsyncWithAggregateResult(model, agg, default);
        Assert.Equal("agg-measure", updated.Measure);
        Assert.Contains(updated.GroupPopulations, g => g.PopulationId == "pop1");
    }

    #endregion

    #region AddWithReportScheduleAsync & AddAsyncWithAggregateResult

    [Fact]
    public async Task AddWithReportScheduleAsync_AddsPopulationsForEachType()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var scheduleId = Guid.NewGuid();
        var schedule = new ReportScheduleModel
        {
            Id = scheduleId,
            FacilityId = "fac-1",
            ReportTypes = new List<string> { "type1", "type2" }
        };

        await SeedReportScheduleAsync(context, scheduleId, schedule.FacilityId);

        var result = await sut.AddWithReportScheduleAsync(schedule, default);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(scheduleId, r.ReportScheduleId));
    }

    [Fact]
    public async Task AddAsyncWithAggregateResult_AddsPopulationWithAggregate()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var scheduleId = Guid.NewGuid();
        var agg = new AggregateMeasureReportResult
        {
            Measure = "agg-measure",
            ReportType = "agg-type",
            MeasureReportId = Guid.NewGuid().ToString(),
            PopulationList = new List<AggregateMeasureReportPopulation>
            {
                new AggregateMeasureReportPopulation { PopulationId = "pop1", PopulationCode = new Hl7.Fhir.Model.CodeableConcept("system","c1"), PopulationCount = 5 }
            }
        };

        await SeedReportScheduleAsync(context, scheduleId, "fac-1");

        var model = await sut.AddAsyncWithAggregateResult("fac-1", scheduleId, agg, default);
        Assert.Equal("agg-measure", model.Measure);
        Assert.Single(model.GroupPopulations);
        Assert.Equal("pop1", model.GroupPopulations[0].PopulationId);
    }

    #endregion

    #region CountNumberOfMeasureReportPopulationsInIP

    [Fact]
    public async Task CountNumberOfMeasureReportPopulationsInIP_ReturnsCorrectCount()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
        var scheduleId = Guid.NewGuid();
        var model = CreatePopulationModel(reportScheduleId: scheduleId, groupPopId: "initial-population");
        model.GroupPopulations[0].MeasureReportPopulations.Add(new MeasureReportPopulationModel { MeasureReportId = "mr1", PopulationCount = 2 });
        await SeedAsync(context, model);

        var count = await sut.CountNumberOfMeasureReportPopulationsInIP(scheduleId, default);
        Assert.Equal(1, count);
    }

    #endregion

    #region MeasureReportPopulation Lifecycle Tests

    [Fact]
    public async Task AddAsyncWithAggregateResult_CreatesMeasureReportPopulations()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var scheduleId = Guid.NewGuid();
        await SeedReportScheduleAsync(context, scheduleId, "fac-1");

        var agg = new AggregateMeasureReportResult
        {
            Measure = "test-measure",
            ReportType = "test-type",
            MeasureReportId = "mr-123",
            PopulationList = new List<AggregateMeasureReportPopulation>
            {
                new AggregateMeasureReportPopulation { PopulationId = "initial-population", PopulationCount = 42 }
            }
        };

        var model = await sut.AddAsyncWithAggregateResult("fac-1", scheduleId, agg, default);

        Assert.Single(model.GroupPopulations);
        var group = model.GroupPopulations[0];
        Assert.Single(group.MeasureReportPopulations);
        Assert.Equal("mr-123", group.MeasureReportPopulations[0].MeasureReportId);
        Assert.Equal(42, group.MeasureReportPopulations[0].PopulationCount);
    }

    [Fact]
    public async Task UpdateAsyncWithAggregateResult_AddsMeasureReportPopulationAndIncrementsTotal()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var scheduleId = Guid.NewGuid();
        var model = CreatePopulationModel(reportScheduleId: scheduleId, groupPopId: "initial-population");

        model.GroupPopulations[0].MeasureReportPopulations.Add(new MeasureReportPopulationModel
        {
            MeasureReportId = "mr-original",
            PopulationCount = 1
        });
        await SeedAsync(context, model);

        var agg = new AggregateMeasureReportResult
        {
            Measure = "updated-measure",
            ReportType = model.ReportType,
            MeasureReportId = "mr-new-456",
            PopulationList = new List<AggregateMeasureReportPopulation>
            {
                new AggregateMeasureReportPopulation { PopulationId = "initial-population", PopulationCount = 10 }
            }
        };

        var updated = await sut.UpdateAsyncWithAggregateResult(model, agg, default);

        var group = updated.GroupPopulations.First(g => g.PopulationId == "initial-population");
        Assert.Equal(11, group.TotalPopulationCount);

        var reloaded = await sut.FindAsync(x => x.Id == model.Id, default);
        var dbGroup = reloaded.First().GroupPopulations.First(g => g.PopulationId == "initial-population");
        Assert.Equal(2, dbGroup.MeasureReportPopulations.Count);
        Assert.Contains(dbGroup.MeasureReportPopulations, m => m.MeasureReportId == "mr-new-456");

        var count = await sut.CountNumberOfMeasureReportPopulationsInIP(scheduleId, default);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task FindAsync_ReturnsMeasureReportPopulations()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var model = CreatePopulationModel(groupPopId: "initial-population");
        model.GroupPopulations[0].MeasureReportPopulations.Add(new MeasureReportPopulationModel
        {
            MeasureReportId = "mr-999",
            PopulationCount = 25
        });
        await SeedAsync(context, model);

        var result = await sut.FindAsync(x => x.Id == model.Id, default);
        Assert.Single(result);
        var group = result[0].GroupPopulations[0];
        Assert.Single(group.MeasureReportPopulations);
        Assert.Equal("mr-999", group.MeasureReportPopulations[0].MeasureReportId);
        Assert.Equal(25, group.MeasureReportPopulations[0].PopulationCount);
    }

    #endregion

    #region Helper Methods

    private static ReportPopulationModel CreatePopulationModel(string? facilityId = null, Guid? reportScheduleId = null, string? groupPopId = null)
    {
        return new ReportPopulationModel
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId ?? Guid.NewGuid().ToString(),
            ReportScheduleId = reportScheduleId ?? Guid.NewGuid(),
            ReportType = "type1",
            Measure = "measure1",
            GroupPopulations =
            {
                new GroupPopulationModel
                {
                    PopulationId = groupPopId ?? "gp1",
                    PopulationCodeJson = JsonSerializer.Serialize(new { code = "c1" }),
                    TotalPopulationCount = 1,
                    MeasureReportPopulations = new List<MeasureReportPopulationModel>()
                }
            }
        };
    }

    private static async Task SeedReportScheduleAsync(ReportDbContext context, Guid scheduleId, string facilityId)
    {
        if (await context.Set<ReportSchedule>().AnyAsync(rs => rs.Id == scheduleId))
            return;

        context.Set<ReportSchedule>().Add(new ReportSchedule
        {
            Id = scheduleId,
            FacilityId = facilityId,
            CreateDate = DateTime.UtcNow,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-30),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(30),
            EnableSubmission = true,
            EndOfReportPeriodJobHasRun = false,
            Frequency = 0,
            Status = 0
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedAsync(ReportDbContext context, params ReportPopulationModel[] models)
    {
        try
        {
            foreach (var model in models)
            {
                await SeedReportScheduleAsync(context, model.ReportScheduleId, model.FacilityId);

                var entity = new ReportPopulation
                {
                    Id = model.Id,
                    FacilityId = model.FacilityId,
                    ReportScheduleId = model.ReportScheduleId,
                    ReportType = model.ReportType,
                    Measure = model.Measure,
                    CreateDate = DateTime.UtcNow,
                    GroupPopulations = model.GroupPopulations.Select(g => new GroupPopulation
                    {
                        PopulationId = g.PopulationId,
                        PopulationCodeJson = g.PopulationCodeJson,
                        TotalPopulationCount = g.TotalPopulationCount,
                        MeasureReportPopulations = g.MeasureReportPopulations.Select(mrp => new MeasureReportPopulation
                        {
                            MeasureReportId = mrp.MeasureReportId,
                            PopulationCount = mrp.PopulationCount
                        }).ToList()
                    }).ToList()
                };
                context.ReportPopulation.Add(entity);
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            throw new Exception("Error seeding ReportPopulation data", ex);
        }
    }

    #endregion
}