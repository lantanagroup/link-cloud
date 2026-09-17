using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DMRP.Business;

/// <summary>
/// Resolves the reporting period a facility is in now, read in its own timezone so a facility
/// near a month boundary is scheduled against the month it is actually in.
/// </summary>
public interface IFacilityReportingPeriodResolver
{
    /// <summary>The period a facility is in now, read in <paramref name="timeZone"/>.</summary>
    ReportingPeriod Resolve(string? facilityId, string? timeZone);

    /// <summary>The period a facility is in now, with the timezone read from the host.</summary>
    Task<ReportingPeriod> ResolveAsync(string facilityId, CancellationToken cancellationToken);
}

public class FacilityReportingPeriodResolver : IFacilityReportingPeriodResolver
{
    private readonly ILogger<FacilityReportingPeriodResolver> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IFacilityTimeZoneSource _facilityTimeZoneSource;

    public FacilityReportingPeriodResolver(
         ILogger<FacilityReportingPeriodResolver> logger, 
         TimeProvider timeProvider, 
         IFacilityTimeZoneSource facilityTimeZoneSource)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _facilityTimeZoneSource = facilityTimeZoneSource ?? throw new ArgumentNullException(nameof(facilityTimeZoneSource));
    }

    public ReportingPeriod Resolve(string? facilityId, string? timeZone)
    {
        // One clock reading for the whole resolution.
        var utcNow = _timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            _logger.LogWarning("Facility {FacilityId} has no timezone; the reporting period was read in UTC instead.",
                facilityId?.SanitizeForLog());

            return ToPeriod(utcNow);
        }

        // Checked rather than caught. The write path resolves the period before the host validates the
        // facility, so this timezone is still whatever the caller sent, and the exception thrown by
        // FindSystemTimeZoneById would carry that value into the log unsanitized. Nothing is thrown
        // here, so there is no exception to log and no unsanitized value to leak.
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone))
        {
            // Fall back rather than fail. On the write path the host's own validation then rejects the
            // timezone with a message naming it; on the read path the caller still gets an answer. The
            // rejected value is left out of the log: the host's response names it on the way in, and a
            // stored one can be read from the facility.
            _logger.LogWarning(
                "Facility {FacilityId} has an unusable timezone; the reporting period was read in UTC instead.",
                facilityId?.SanitizeForLog());

            return ToPeriod(utcNow);
        }

        return ToPeriod(TimeZoneInfo.ConvertTime(utcNow, zone));
    }

    public async Task<ReportingPeriod> ResolveAsync(string facilityId, CancellationToken cancellationToken)
    {
        var timeZone = await _facilityTimeZoneSource.GetTimeZoneAsync(facilityId, cancellationToken);
        if (timeZone is null)
        {
            // No such facility. Not a data problem, so no warning.
            _logger.LogDebug("No facility {FacilityId}; reporting period read in UTC.", facilityId.SanitizeForLog());
            return UtcPeriod();
        }

        // Exists: same logic as the write path, including the warning for a blank or bad zone.
        return Resolve(facilityId, timeZone);
    }

    private ReportingPeriod UtcPeriod() => ToPeriod(_timeProvider.GetUtcNow());

    private static ReportingPeriod ToPeriod(DateTimeOffset instant) => new(instant.Year, instant.Month);
}

