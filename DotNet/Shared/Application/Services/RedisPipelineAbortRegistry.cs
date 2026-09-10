using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LantanaGroup.Link.Shared.Application.Services;

public sealed class RedisPipelineAbortRegistry(
    IConnectionMultiplexer multiplexer,
    ILogger<RedisPipelineAbortRegistry> logger) : IPipelineAbortRegistry
{
    public async Task AbortAsync(string? facilityId, string? reportId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        if (timeToLive <= TimeSpan.Zero)
            timeToLive = TimeSpan.FromDays(14);

        var db = multiplexer.GetDatabase();
        try
        {
            var facilityKey = InMemoryPipelineAbortRegistry.FacilityKey(facilityId);
            if (facilityKey != null)
                await db.StringSetAsync(facilityKey, "1", timeToLive);

            var reportKey = InMemoryPipelineAbortRegistry.ReportKey(reportId);
            if (reportKey != null)
                await db.StringSetAsync(reportKey, "1", timeToLive);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write pipeline abort flag for facility {FacilityId}, report {ReportId}.", facilityId, reportId);
        }
    }

    public async Task<bool> IsAbortedAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = multiplexer.GetDatabase();
            var facilityKey = InMemoryPipelineAbortRegistry.FacilityKey(facilityId);
            if (facilityKey != null && await db.KeyExistsAsync(facilityKey))
                return true;

            var reportKey = InMemoryPipelineAbortRegistry.ReportKey(reportId);
            if (reportKey != null && await db.KeyExistsAsync(reportKey))
                return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read pipeline abort flag for facility {FacilityId}, report {ReportId}.", facilityId, reportId);
        }

        return false;
    }

    public async Task ClearAsync(string? facilityId, string? reportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = multiplexer.GetDatabase();
            var facilityKey = InMemoryPipelineAbortRegistry.FacilityKey(facilityId);
            if (facilityKey != null)
                await db.KeyDeleteAsync(facilityKey);

            var reportKey = InMemoryPipelineAbortRegistry.ReportKey(reportId);
            if (reportKey != null)
                await db.KeyDeleteAsync(reportKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear pipeline abort flag for facility {FacilityId}, report {ReportId}.", facilityId, reportId);
        }
    }
}
