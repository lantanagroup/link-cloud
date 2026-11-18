using DataAcquisition.Domain.Migrations;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Migrations;

/// <summary>
/// The SQL that fills per-log resource-type counts for logs saved before the counts were stored.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ResourceTypeCountBackfillTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public ResourceTypeCountBackfillTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BackfillSql_LogsWithOnlyResourceIds_MatchesTheCountsTheServiceStores()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        string[] resourceIds =
        [
            "Observation/1",
            "Observation/2",
            "observation/3",
            "Condition/c1",
            "bare-id",
            "/leading-slash",
            "Patient/",
            ""
        ];
        var log = await AddLogWithResourceIdsAsync(dbContext, resourceIds);

        await dbContext.Database.ExecuteSqlRawAsync(StoreResourceTypeCountsPerLog.BackfillSql);

        var expected = resourceIds
            .ToResourceTypeCounts(log.Id)
            .ToDictionary(c => c.ResourceType, c => c.Count, StringComparer.OrdinalIgnoreCase);
        var actual = await GetResourceTypeCountsAsync(dbContext, log.Id);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task BackfillSql_RunTwice_LeavesLogsThatAlreadyHaveCountsAlone()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var backfilled = await AddLogWithResourceIdsAsync(dbContext, ["Observation/1", "Observation/2"]);
        var alreadyCounted = await AddLogWithResourceIdsAsync(dbContext, ["Encounter/e1"]);
        dbContext.DataAcquisitionLogResourceTypeCounts.Add(new DataAcquisitionLogResourceTypeCount
        {
            DataAcquisitionLogId = alreadyCounted.Id,
            ResourceType = "Encounter",
            Count = 5
        });
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlRawAsync(StoreResourceTypeCountsPerLog.BackfillSql);
        await dbContext.Database.ExecuteSqlRawAsync(StoreResourceTypeCountsPerLog.BackfillSql);

        Assert.Equal(new Dictionary<string, int> { ["Observation"] = 2 },
                     await GetResourceTypeCountsAsync(dbContext, backfilled.Id));
        Assert.Equal(new Dictionary<string, int> { ["Encounter"] = 5 },
                     await GetResourceTypeCountsAsync(dbContext, alreadyCounted.Id));
    }

    private static async Task<DataAcquisitionLog> AddLogWithResourceIdsAsync(DataAcquisitionDbContext dbContext,
                                                                             string[] resourceIds)
    {
        var log = new DataAcquisitionLog
        {
            FacilityId = $"Backfill_{Guid.NewGuid():N}",
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ResourceIds = resourceIds
                .Select(id => new DataAcquisitionLogResourceId { ResourceId = id })
                .ToList()
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        return log;
    }

    private static async Task<Dictionary<string, int>> GetResourceTypeCountsAsync(DataAcquisitionDbContext dbContext,
                                                                                long logId)
    {
        return await dbContext.DataAcquisitionLogResourceTypeCounts
            .AsNoTracking()
            .Where(c => c.DataAcquisitionLogId == logId)
            .ToDictionaryAsync(c => c.ResourceType, c => c.Count, StringComparer.OrdinalIgnoreCase);
    }
}
