using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportEntryManagerTests
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportEntryManagerTests(ReportIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    #region Argument Validation Tests

    [Fact]
    public async Task AddRangeAsync_WithNullModels_DoesNothing()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var countBefore = await context.ReportEntry.CountAsync();

        await sut.AddRangeAsync(null!, default);

        var countAfter = await context.ReportEntry.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task AddRangeAsync_WithEmptyModels_DoesNothing()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var countBefore = await context.ReportEntry.CountAsync();

        await sut.AddRangeAsync(Array.Empty<ReportEntryModel>(), default);

        var countAfter = await context.ReportEntry.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task UpdateAsync_WithNonexistentId_Throws()
    {
        using var scope = _scopeFactory.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
        var model = new ReportEntryModel { Id = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(model, default));
    }

    #endregion

    #region Add & Find Tests

    [Fact]
    public async Task AddAsync_AddsEntry()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = "fac-entry-add";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);

        await sut.AddAsync(model, default);

        var entity = await context.ReportEntry.FirstOrDefaultAsync(x => x.Id == model.Id);
        Assert.NotNull(entity);
        Assert.Equal(facilityId, entity.FacilityId);
    }

    [Fact]
    public async Task AddRangeAsync_AddsMultipleEntries()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = "fac-entry-multi";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var models = new[]
        {
            CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "patient-1"),
            CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "patient-2")
        };

        var countBefore = await context.ReportEntry.CountAsync();

        await sut.AddRangeAsync(models, default);

        Assert.Equal(countBefore + 2, await context.ReportEntry.CountAsync());
    }

    [Fact]
    public async Task FindAsync_ReturnsMatchingEntries()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
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
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model);

        var found = await sut.SingleOrDefaultAsync(x => x.Id == model.Id, default);
        Assert.NotNull(found);
        Assert.Equal(model.Id, found!.Id);

        var nonExistentId = Guid.NewGuid();
        var notFound = await sut.SingleOrDefaultAsync(x => x.Id == nonExistentId, default);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task GetEntry_ReturnsEntryByScheduleAndPatient()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var patientId = "patient-get-entry";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: patientId);
        await SeedAsync(context, model);

        var found = await sut.GetEntry(scheduleId, patientId, default);
        Assert.NotNull(found);
        Assert.Equal(patientId, found!.PatientId);
        Assert.Equal(scheduleId, found.ReportScheduleId);
    }

    [Fact]
    public async Task GetEntry_ReturnsNullForNonexistent()
    {
        using var scope = _scopeFactory.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var result = await sut.GetEntry(Guid.NewGuid(), "nonexistent-patient", default);
        Assert.Null(result);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_UpdatesEntry()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model);

        model.ReportingStatus = ReportingStatus.PassedValidation;
        var updated = await sut.UpdateAsync(model, default);
        Assert.Equal(ReportingStatus.PassedValidation, updated.ReportingStatus);

        var entity = await context.ReportEntry.FirstAsync(x => x.Id == model.Id);
        Assert.Equal(ReportingStatus.PassedValidation, entity.ReportingStatus);
    }

    [Fact]
    public async Task UpdateAsyncNotReportableEntry_SetsNotReportableStatus()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model);

        var updated = await sut.UpdateAsyncNotReportableEntry(model, default);
        Assert.Equal(ReportingStatus.NotReportable, updated.ReportingStatus);
        Assert.Equal(SubmissionStatus.NotEligable, updated.SubmissionStatus);
    }

    #endregion

    #region MeasureReport Lifecycle Tests

    [Fact]
    public async Task AddAsync_WithMeasureReports_CreatesMeasureReportsAndResourceCounts()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "type1",
            Status = MeasureReportStatus.EntryCreated,
            ResourceCount = new Dictionary<string, int> { { "Patient", 5 }, { "Encounter", 3 } }
        });

        await sut.AddAsync(model, default);

        var entity = await context.ReportEntry
            .Include(e => e.MeasureReports)
            .ThenInclude(m => m.ResourceCounts)
            .FirstOrDefaultAsync(x => x.Id == model.Id);

        Assert.NotNull(entity);
        Assert.Single(entity!.MeasureReports);
        var mr = entity.MeasureReports.First();
        Assert.Equal("type1", mr.ReportType);
        Assert.Equal(2, mr.ResourceCounts.Count);
    }

    [Fact]
    public async Task FindAsync_ReturnsMeasureReportsAndResourceCounts()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "type-find",
            Status = MeasureReportStatus.ReadyForValidation,
            MeasureReportId = "mr-find-1",
            ResourceCount = new Dictionary<string, int> { { "Patient", 10 } }
        });
        await SeedAsync(context, model);

        var result = await sut.FindAsync(x => x.Id == model.Id, default);
        Assert.Single(result);
        Assert.Single(result[0].MeasureReports);
        Assert.Equal("type-find", result[0].MeasureReports[0].ReportType);
        Assert.Equal("mr-find-1", result[0].MeasureReports[0].MeasureReportId);
        Assert.Equal(10, result[0].MeasureReports[0].ResourceCount["Patient"]);
    }

    [Fact]
    public async Task UpdateAsync_AddsMeasureReportToExistingEntry()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "type-original",
            Status = MeasureReportStatus.EntryCreated,
            ResourceCount = new Dictionary<string, int>()
        });
        await SeedAsync(context, model);

        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "type-new",
            Status = MeasureReportStatus.EntryCreated,
            ResourceCount = new Dictionary<string, int> { { "Observation", 7 } }
        });

        await sut.UpdateAsync(model, default);

        var entity = await context.ReportEntry
            .Include(e => e.MeasureReports)
            .ThenInclude(m => m.ResourceCounts)
            .FirstAsync(x => x.Id == model.Id);

        Assert.Equal(2, entity.MeasureReports.Count);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingMeasureReport()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "type-update",
            Status = MeasureReportStatus.EntryCreated,
            MeasureReportId = "mr-old",
            ResourceCount = new Dictionary<string, int> { { "Patient", 1 } }
        });
        await SeedAsync(context, model);

        model.MeasureReports[0].MeasureReportId = "mr-updated";
        model.MeasureReports[0].Status = MeasureReportStatus.ReadyForValidation;
        model.MeasureReports[0].ResourceCount = new Dictionary<string, int> { { "Patient", 5 }, { "Encounter", 2 } };

        await sut.UpdateAsync(model, default);

        var entity = await context.ReportEntry
            .Include(e => e.MeasureReports)
            .ThenInclude(m => m.ResourceCounts)
            .FirstAsync(x => x.Id == model.Id);

        Assert.Single(entity.MeasureReports);
        var mr = entity.MeasureReports.First();
        Assert.Equal("mr-updated", mr.MeasureReportId);
        Assert.Equal(MeasureReportStatus.ReadyForValidation, mr.Status);
        Assert.Equal(2, mr.ResourceCounts.Count);
    }

    #endregion

    #region UpdateAsyncWithConsumerResult Tests

    [Fact]
    public async Task UpdateAsyncWithConsumerResult_UpdatesMeasureReportFields()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var patientId = "patient-consumer";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: patientId);
        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "consumer-type",
            Status = MeasureReportStatus.EntryCreated,
            ResourceCount = new Dictionary<string, int>()
        });
        await SeedAsync(context, model);

        var consumerValue = new MeasureReportGeneratedValue
        {
            FacilityId = facilityId,
            ReportTrackingId = scheduleId.ToString(),
            PatientId = patientId,
            ReportType = "consumer-type",
            MeasureReportId = "mr-consumer-123",
            MeasureReportURI = "https://example.com/mr",
            MeasureReportBlobName = "blob-name",
            Reportable = true
        };

        var updated = await sut.UpdateAsyncWithConsumerResult(consumerValue);

        Assert.Equal(MeasureReportStatus.ReadyForValidation, updated.MeasureReports.First(x => x.ReportType == "consumer-type").Status);
        Assert.Equal("mr-consumer-123", updated.MeasureReports.First(x => x.ReportType == "consumer-type").MeasureReportId);
    }

    [Fact]
    public async Task UpdateAsyncWithConsumerResult_NotReportable_SetsNotReportableStatus()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        var patientId = "patient-not-reportable";
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: patientId);
        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "nr-type",
            Status = MeasureReportStatus.EntryCreated,
            ResourceCount = new Dictionary<string, int>()
        });
        await SeedAsync(context, model);

        var consumerValue = new MeasureReportGeneratedValue
        {
            FacilityId = facilityId,
            ReportTrackingId = scheduleId.ToString(),
            PatientId = patientId,
            ReportType = "nr-type",
            MeasureReportId = "mr-nr-123",
            MeasureReportURI = "https://example.com/mr",
            MeasureReportBlobName = "blob-name",
            Reportable = false
        };

        var updated = await sut.UpdateAsyncWithConsumerResult(consumerValue);

        Assert.Equal(MeasureReportStatus.NotReportable, updated.MeasureReports.First(x => x.ReportType == "nr-type").Status);
    }

    #endregion

    #region UpdateAsyncWithAggregateResult Tests

    [Fact]
    public async Task UpdateAsyncWithAggregateResult_UpdatesAggregateFieldsAndResourceCounts()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        model.MeasureReports.Add(new EntryMeasureReportModel
        {
            ReportType = "agg-type",
            Status = MeasureReportStatus.ReadyForValidation,
            ResourceCount = new Dictionary<string, int>()
        });
        await SeedAsync(context, model);

        var aggResult = new AggregateResult
        {
            Uri = new Uri("https://example.com/aggregate"),
            BlobName = "agg-blob",
            MeasureReportResults = new List<AggregateMeasureReportResult>
            {
                new AggregateMeasureReportResult
                {
                    ReportType = "agg-type",
                    Measure = "agg-measure",
                    MeasureReportId = "agg-mr-id",
                    ResourceCount = new Dictionary<string, int> { { "Patient", 3 }, { "Encounter", 1 } }
                }
            }
        };

        var updated = await sut.UpdateAsyncWithAggregateResult(model, aggResult, default);

        Assert.Equal("https://example.com/aggregate", updated.AggregateReportUri);
        Assert.Equal("agg-blob", updated.AggregateReportBlobName);
        var mr = updated.MeasureReports.First(x => x.ReportType == "agg-type");
        Assert.Equal(3, mr.ResourceCount["Patient"]);
        Assert.Equal(1, mr.ResourceCount["Encounter"]);
    }

    #endregion

    #region CountAsync Tests

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model1 = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "count-p1");
        var model2 = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "count-p2");
        await SeedAsync(context, model1, model2);

        var count = await sut.CountAsync(x => x.ReportScheduleId == scheduleId, default);
        Assert.Equal(2, count);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ReturnsPaginatedResults()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        for (int i = 0; i < 5; i++)
        {
            var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: $"search-p-{i}");
            await SeedAsync(context, model);
        }

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            patientId: null,
            reportScheduleId: scheduleId,
            reportingStatuses: null,
            submissionStatuses: null,
            submissionStatusIsNull: false,
            reportType: null,
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
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model);

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            patientId: null,
            reportScheduleId: null,
            reportingStatuses: null,
            submissionStatuses: null,
            submissionStatusIsNull: false,
            reportType: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default);

        Assert.All(result.Records, r => Assert.Equal(facilityId, r.FacilityId));
    }

    [Fact]
    public async Task SearchAsync_FiltersByReportingStatus()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var modelIdentified = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "status-p1");
        modelIdentified.ReportingStatus = ReportingStatus.PatientIdentified;
        await SeedAsync(context, modelIdentified);

        var modelNotReportable = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "status-p2");
        modelNotReportable.ReportingStatus = ReportingStatus.NotReportable;
        await SeedAsync(context, modelNotReportable);

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            patientId: null,
            reportScheduleId: scheduleId,
            reportingStatuses: new List<ReportingStatus> { ReportingStatus.PatientIdentified },
            submissionStatuses: null,
            submissionStatusIsNull: false,
            reportType: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default);

        Assert.All(result.Records, r => Assert.Equal(ReportingStatus.PatientIdentified, r.ReportingStatus));
    }

    [Fact]
    public async Task SearchAsync_DeletedSchedule_ReturnsEmptyResults()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId, isDeleted: true);

        var model = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId);
        await SeedAsync(context, model, skipScheduleSeed: true);

        var result = await sut.SearchAsync(
            facilityId: facilityId,
            patientId: null,
            reportScheduleId: scheduleId,
            reportingStatuses: null,
            submissionStatuses: null,
            submissionStatusIsNull: false,
            reportType: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: default);

        Assert.Empty(result.Records);
    }

    #endregion

    #region GetSummaryByReportScheduleIdAsync Tests

    [Fact]
    public async Task GetSummaryByReportScheduleIdAsync_ReturnsCorrectCounts()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var model1 = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "summary-p1");
        model1.ReportingStatus = ReportingStatus.PatientIdentified;
        model1.MeasureReports.Add(new EntryMeasureReportModel { ReportType = "type-a", ResourceCount = new Dictionary<string, int>() });
        await SeedAsync(context, model1);

        var model2 = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "summary-p2");
        model2.ReportingStatus = ReportingStatus.PatientIdentified;
        model2.MeasureReports.Add(new EntryMeasureReportModel { ReportType = "type-a", ResourceCount = new Dictionary<string, int>() });
        await SeedAsync(context, model2);

        var model3 = CreateEntryModel(facilityId: facilityId, reportScheduleId: scheduleId, patientId: "summary-p3");
        model3.ReportingStatus = ReportingStatus.NotReportable;
        model3.SubmissionStatus = SubmissionStatus.NotEligable;
        model3.MeasureReports.Add(new EntryMeasureReportModel { ReportType = "type-a", ResourceCount = new Dictionary<string, int>() });
        await SeedAsync(context, model3);

        var summary = await sut.GetSummaryByReportScheduleIdAsync(scheduleId, default);

        Assert.Equal(2, summary.ReportingStatusCounts["PatientIdentified"]);
        Assert.Equal(1, summary.ReportingStatusCounts["NotReportable"]);
        Assert.Equal(2, summary.SubmissionStatusCounts["Pending"]);
        Assert.Equal(1, summary.SubmissionStatusCounts["NotEligable"]);
        Assert.Equal(2, summary.ReportTypeCounts["type-a"]);
    }

    #endregion

    #region Helper Methods

    private static ReportEntryModel CreateEntryModel(
        string? facilityId = null,
        Guid? reportScheduleId = null,
        string? patientId = null)
    {
        return new ReportEntryModel
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId ?? Guid.NewGuid().ToString(),
            ReportScheduleId = reportScheduleId ?? Guid.NewGuid(),
            PatientId = patientId ?? Guid.NewGuid().ToString(),
            ReportingStatus = ReportingStatus.PatientIdentified,
            MeasureReports = new List<EntryMeasureReportModel>()
        };
    }

    private static async Task SeedReportScheduleAsync(ReportDbContext context, Guid scheduleId, string facilityId, bool isDeleted = false)
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
            Status = 0,
            IsDeleted = isDeleted
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedAsync(ReportDbContext context, params ReportEntryModel[] models)
    {
        await SeedAsync(context, false, models);
    }

    private static async Task SeedAsync(ReportDbContext context, ReportEntryModel model, bool skipScheduleSeed)
    {
        await SeedAsync(context, skipScheduleSeed, new[] { model });
    }

    private static async Task SeedAsync(ReportDbContext context, bool skipScheduleSeed, params ReportEntryModel[] models)
    {
        try
        {
            foreach (var model in models)
            {
                if (!skipScheduleSeed)
                    await SeedReportScheduleAsync(context, model.ReportScheduleId, model.FacilityId);

                var entity = new ReportEntry
                {
                    Id = model.Id,
                    FacilityId = model.FacilityId,
                    ReportScheduleId = model.ReportScheduleId,
                    PatientId = model.PatientId,
                    ReportingStatus = model.ReportingStatus,
                    SubmissionStatus = model.SubmissionStatus,
                    CreateDate = DateTime.UtcNow,
                    MeasureReports = model.MeasureReports.Select(m => new EntryMeasureReport
                    {
                        ReportType = m.ReportType,
                        Status = m.Status,
                        MeasureReportId = m.MeasureReportId,
                        MeasureReportUri = m.MeasureReportUri,
                        MeasureReportFileName = m.MeasureReportFileName,
                        ResourceCounts = m.ResourceCount.Select(kv => new ResourceCounts
                        {
                            ResourceType = kv.Key,
                            ResourceCount = kv.Value
                        }).ToList()
                    }).ToList()
                };
                context.ReportEntry.Add(entity);
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            throw new Exception("Error seeding ReportEntry data", ex);
        }
    }

    #endregion
}
