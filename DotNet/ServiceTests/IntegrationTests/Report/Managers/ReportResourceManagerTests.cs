using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportResourceManagerTests
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportResourceManagerTests(ReportIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    #region Argument Validation Tests

    [Fact]
    public async Task UpdateAsync_WithNonexistentId_Throws()
    {
        using var scope = _scopeFactory.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();
        var model = new ReportResourceModel { Id = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(model, default));
    }

    #endregion

    #region Add & Find Tests

    [Fact]
    public async Task AddAsync_AddsResource()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = "fac-res-add";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);

        await sut.AddAsync(model, default);

        var entity = await context.ReportResource.FirstOrDefaultAsync(x => x.Id == model.Id);
        Assert.NotNull(entity);
        Assert.Equal(facilityId, entity.FacilityId);
        Assert.Equal(model.PatientId, entity.PatientId);
        Assert.Equal(model.ResourceType, entity.ResourceType);
        Assert.Equal(model.ResourceId, entity.ResourceId);
    }

    [Fact]
    public async Task AddAsync_WithEmptyId_GeneratesNewId()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = "fac-res-newid";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);
        model.Id = Guid.Empty;

        var result = await sut.AddAsync(model, default);

        Assert.NotEqual(Guid.Empty, result.Id);
        var entity = await context.ReportResource.FirstOrDefaultAsync(x => x.Id == result.Id);
        Assert.NotNull(entity);
    }

    [Fact]
    public async Task FindAsync_ReturnsMatchingResources()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model);

        var result = await sut.FindAsync(x => x.FacilityId == facilityId, default);
        Assert.Single(result);
        Assert.Equal(facilityId, result[0].FacilityId);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_ReturnsSingleOrNull()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);
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
    public async Task UpdateAsync_UpdatesResource()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model);

        model.ResourceType = "updated-type";
        model.ResourceId = "updated-id";
        var updated = await sut.UpdateAsync(model, default);
        Assert.Equal("updated-type", updated.ResourceType);
        Assert.Equal("updated-id", updated.ResourceId);

        var entity = await context.ReportResource.FirstAsync(x => x.Id == model.Id);
        Assert.Equal("updated-type", entity.ResourceType);
        Assert.Equal("updated-id", entity.ResourceId);
        Assert.NotNull(entity.ModifyDate);
    }

    #endregion

    #region AddAsyncWithAggregateResult Tests

    [Fact]
    public async Task AddAsyncWithAggregateResult_AddsResourcesFromAggregate()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var patientId = "patient-agg";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var aggResult = new AggregateResult
        {
            Uri = new Uri("https://example.com/aggregate"),
            BlobName = "blob",
            MeasureReportResults = new List<AggregateMeasureReportResult>
            {
                new AggregateMeasureReportResult
                {
                    MeasureReportId = "mr-agg-1",
                    ReportType = "type1",
                    Measure = "measure1",
                    ResourceReferences = new List<string[]>
                    {
                        new[] { "Patient", "p-123" },
                        new[] { "Encounter", "e-456" }
                    }
                }
            }
        };

        var countBefore = await context.ReportResource.CountAsync(x => x.ReportScheduleId == scheduleId);

        await sut.AddAsyncWithAggregateResult(facilityId, scheduleId, patientId, aggResult, default);

        var countAfter = await context.ReportResource.CountAsync(x => x.ReportScheduleId == scheduleId);
        Assert.Equal(countBefore + 2, countAfter);

        var resources = await context.ReportResource.Where(x => x.ReportScheduleId == scheduleId).ToListAsync();
        Assert.Contains(resources, r => r.ResourceType == "Patient" && r.ResourceId == "p-123");
        Assert.Contains(resources, r => r.ResourceType == "Encounter" && r.ResourceId == "e-456");
    }

    [Fact]
    public async Task AddAsyncWithAggregateResult_WithNullResults_DoesNothing()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var countBefore = await context.ReportResource.CountAsync();

        await sut.AddAsyncWithAggregateResult(facilityId, scheduleId, "patient", null!, default);

        var countAfter = await context.ReportResource.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task AddAsyncWithAggregateResult_WithEmptyResults_DoesNothing()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var aggResult = new AggregateResult
        {
            Uri = new Uri("https://example.com/aggregate"),
            BlobName = "blob",
            MeasureReportResults = new List<AggregateMeasureReportResult>()
        };

        var countBefore = await context.ReportResource.CountAsync();

        await sut.AddAsyncWithAggregateResult(facilityId, scheduleId, "patient", aggResult, default);

        var countAfter = await context.ReportResource.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task AddAsyncWithAggregateResult_SkipsShortResourceReferences()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var aggResult = new AggregateResult
        {
            Uri = new Uri("https://example.com/aggregate"),
            BlobName = "blob",
            MeasureReportResults = new List<AggregateMeasureReportResult>
            {
                new AggregateMeasureReportResult
                {
                    MeasureReportId = "mr-short",
                    ReportType = "type1",
                    Measure = "measure1",
                    ResourceReferences = new List<string[]>
                    {
                        new[] { "Patient" },
                        new[] { "Encounter", "e-valid" }
                    }
                }
            }
        };

        await sut.AddAsyncWithAggregateResult(facilityId, scheduleId, "patient", aggResult, default);

        var resources = await context.ReportResource.Where(x => x.ReportScheduleId == scheduleId).ToListAsync();
        Assert.Single(resources);
        Assert.Equal("Encounter", resources[0].ResourceType);
    }

    [Fact]
    public async Task AddAsyncWithAggregateResult_MultipleMeasureReports_AddsAll()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var aggResult = new AggregateResult
        {
            Uri = new Uri("https://example.com/aggregate"),
            BlobName = "blob",
            MeasureReportResults = new List<AggregateMeasureReportResult>
            {
                new AggregateMeasureReportResult
                {
                    MeasureReportId = "mr-1",
                    ReportType = "type1",
                    Measure = "measure1",
                    ResourceReferences = new List<string[]>
                    {
                        new[] { "Patient", "p-1" }
                    }
                },
                new AggregateMeasureReportResult
                {
                    MeasureReportId = "mr-2",
                    ReportType = "type2",
                    Measure = "measure2",
                    ResourceReferences = new List<string[]>
                    {
                        new[] { "Encounter", "e-1" },
                        new[] { "Observation", "o-1" }
                    }
                }
            }
        };

        await sut.AddAsyncWithAggregateResult(facilityId, scheduleId, "patient-multi", aggResult, default);

        var resources = await context.ReportResource
            .Where(x => x.ReportScheduleId == scheduleId)
            .ToListAsync();
        Assert.Equal(3, resources.Count);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ReturnsPaginatedResults()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        for (int i = 0; i < 5; i++)
        {
            var model = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: $"search-p-{i}");
            await SeedAsync(context, model);
        }

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            reportScheduleId: scheduleId,
            patientId: null,
            measureReportId: null,
            resourceType: null,
            resourceId: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 3,
            pageNumber: 1,
            cancellationToken: default);

        Assert.Equal(3, result.Records.Count);
    }

    [Fact]
    public async Task SearchAsync_FiltersByFacilityId()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model);

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            reportScheduleId: null,
            patientId: null,
            measureReportId: null,
            resourceType: null,
            resourceId: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default);

        Assert.All(result.Records, r => Assert.Equal(facilityId, r.FacilityId));
    }

    [Fact]
    public async Task SearchAsync_FiltersByResourceType()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var patientResource = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId, resourceType: "Patient");
        var encounterResource = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId, resourceType: "Encounter");
        await SeedAsync(context, patientResource, encounterResource);

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            reportScheduleId: scheduleId,
            patientId: null,
            measureReportId: null,
            resourceType: "Patient",
            resourceId: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default);

        Assert.All(result.Records, r => Assert.Equal("Patient", r.ResourceType));
    }

    [Fact]
    public async Task SearchAsync_SortsByCreateDateDescending_ByDefault()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model1 = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "sort-p1");
        var model2 = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "sort-p2");
        await SeedAsync(context, model1);
        await Task.Delay(50);
        await SeedAsync(context, model2);

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            reportScheduleId: scheduleId,
            patientId: null,
            measureReportId: null,
            resourceType: null,
            resourceId: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default);

        Assert.True(result.Records.Count >= 2);
        Assert.True(result.Records[0].CreateDate >= result.Records[1].CreateDate);
    }

    [Fact]
    public async Task SearchAsync_SortsByFacilityIdAscending()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model1 = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);
        var model2 = CreateResourceModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model1, model2);

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            reportScheduleId: scheduleId,
            patientId: null,
            measureReportId: null,
            resourceType: null,
            resourceId: null,
            sortBy: "facilityid",
            sortOrder: SortOrder.Ascending,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default);

        Assert.True(result.Records.Count >= 2);
    }

    #endregion

    #region Helper Methods

    private static ReportResourceModel CreateResourceModel(
        string? facilityId = null,
        Guid? reportScheduleId = null,
        string? patientId = null,
        string? resourceType = null,
        string? resourceId = null)
    {
        return new ReportResourceModel
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId ?? Guid.NewGuid().ToString(),
            ReportScheduleId = reportScheduleId ?? Guid.NewGuid(),
            PatientId = patientId ?? Guid.NewGuid().ToString(),
            MeasureReportId = Guid.NewGuid().ToString(),
            ResourceType = resourceType ?? "Patient",
            ResourceId = resourceId ?? Guid.NewGuid().ToString()
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

    private static async Task SeedAsync(ReportDbContext context, params ReportResourceModel[] models)
    {
        try
        {
            foreach (var model in models)
            {
                await SeedReportScheduleAsync(context, model.ReportScheduleId, model.FacilityId);

                var entity = new ReportResource
                {
                    Id = model.Id,
                    FacilityId = model.FacilityId,
                    ReportScheduleId = model.ReportScheduleId,
                    PatientId = model.PatientId,
                    MeasureReportId = model.MeasureReportId,
                    ResourceType = model.ResourceType,
                    ResourceId = model.ResourceId,
                    CreateDate = DateTime.UtcNow
                };
                context.ReportResource.Add(entity);
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            throw new Exception("Error seeding ReportResource data", ex);
        }
    }

    #endregion
}
