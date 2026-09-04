using System.Xml;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.FacilityAdministration;

public class FacilityAdministrationService : IFacilityAdministrationService
{
    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly IFacilityWriteLock _writeLock;
    private readonly IFacilityGateway _facilityGateway;
    private readonly IFhirConfigurationGateway _fhirConfigurationGateway;
    private readonly IQueryDispatchGateway _queryDispatchGateway;

    public FacilityAdministrationService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IFacilityWriteLock writeLock,
        IFacilityGateway facilityGateway,
        IFhirConfigurationGateway fhirConfigurationGateway,
        IQueryDispatchGateway queryDispatchGateway)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _writeLock = writeLock;
        _facilityGateway = facilityGateway;
        _fhirConfigurationGateway = fhirConfigurationGateway;
        _queryDispatchGateway = queryDispatchGateway;
    }

    public async Task<FacilitySummaryResponse?> UpdateFacilityOnboardingAsync(string facilityId, UpdateFacilityOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsFacilityAdmin)
        {
            throw new InvalidOperationException("FACADMIN is required to update facility onboarding.");
        }

        if (!string.Equals(facilityId, _userContext.RequireFacilityId(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Facility onboarding may only be updated for the authenticated facility context.");
        }

        await using var writeLock = await _writeLock.AcquireAsync(facilityId, cancellationToken);

        var facility = await _dbContext.Facilities.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (facility is null)
        {
            return null;
        }

        ApplyOnboardingFlag(facility, request.IsOnboarded);
        facility.LastModifiedOn = DateTime.UtcNow;
        facility.LastModifiedBy = _userContext.ExternalUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await writeLock.CommitAsync(cancellationToken);

        return new FacilitySummaryResponse
        {
            Id = facility.Id,
            FacilityId = facility.FacilityId,
            IsOnboarded = facility.IsOnboarded
        };
    }

    // A facility that was never complete is left alone entirely.
    private static void ApplyOnboardingFlag(NhsnFacility facility, bool isOnboarded)
    {
        if (isOnboarded)
        {
            facility.OnboardingStatus = OnboardingStatus.Complete;
            facility.CompletedOn ??= DateTime.UtcNow;
            return;
        }

        if (facility.OnboardingStatus == OnboardingStatus.Complete)
        {
            facility.OnboardingStatus = OnboardingStatus.InProgress;
            facility.CompletedOn = null;
        }
    }

    public async Task<FhirServerInfoResponse?> GetFhirServerInfoAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var fhirConfig = await _fhirConfigurationGateway.GetAsync(facilityId, cancellationToken);
        var lagDuration = await _queryDispatchGateway.GetLagDurationAsync(facilityId, cancellationToken);
        var (lagDays, lagHours, lagMinutes) = ParseLagDuration(lagDuration);

        // fhirConfig's pull times arrive from Data Acquisition as a "date-span" wire value (e.g.
        // "08:00:00"), not the two-digit HH:MM this response contracts for — reparse before formatting.
        TimeSpan? minPullTime = TimeSpan.TryParse(fhirConfig?.MinAcquisitionPullTime, System.Globalization.CultureInfo.InvariantCulture, out var parsedMin) ? parsedMin : null;
        TimeSpan? maxPullTime = TimeSpan.TryParse(fhirConfig?.MaxAcquisitionPullTime, System.Globalization.CultureInfo.InvariantCulture, out var parsedMax) ? parsedMax : null;

        return new FhirServerInfoResponse
        {
            FhirServerBaseUrl = fhirConfig?.FhirServerBaseUrl,
            MaxConcurrentRequests = fhirConfig?.MaxConcurrentRequests,
            MaxRetries = fhirConfig?.MaxRetries,
            MinAcquisitionPullTime = FormatPullTime(minPullTime),
            MaxAcquisitionPullTime = FormatPullTime(maxPullTime),
            LagDays = lagDays,
            LagHours = lagHours,
            LagMinutes = lagMinutes
        };
    }

    public async Task<FhirServerInfoResponse?> UpdateFhirServerInfoAsync(string facilityId, UpdateFhirServerInfoRequest request, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(request.FhirServerBaseUrl, UriKind.Absolute, out var parsedBaseUrl) ||
            (parsedBaseUrl.Scheme != Uri.UriSchemeHttp && parsedBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("FhirServerBaseUrl must be a valid absolute URL using http or https.");
        }

        if (request.MaxConcurrentRequests < 1)
        {
            throw new InvalidOperationException("MaxConcurrentRequests must be 1 or greater.");
        }

        if (request.MaxRetries is < 0 or > 10)
        {
            throw new InvalidOperationException("MaxRetries must be between 0 and 10.");
        }

        if (request.LagHours is < 0 or > 23)
        {
            throw new InvalidOperationException("Lag hours must be between 0 and 23.");
        }

        if (request.LagMinutes is < 0 or > 59)
        {
            throw new InvalidOperationException("Lag minutes must be between 0 and 59.");
        }

        if (request.LagDays < 0)
        {
            throw new InvalidOperationException("Lag days must be 0 or greater.");
        }

        var minPullTime = ParsePullTime(request.MinAcquisitionPullTime, "MinAcquisitionPullTime");
        var maxPullTime = ParsePullTime(request.MaxAcquisitionPullTime, "MaxAcquisitionPullTime");

        var facility = await _facilityGateway.GetAsync(facilityId, cancellationToken);
        if (facility is null)
        {
            return null;
        }

        await _fhirConfigurationGateway.SaveAsync(new FhirConfigurationSave
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = request.FhirServerBaseUrl,
            MaxConcurrentRequests = request.MaxConcurrentRequests,
            MaxRetries = request.MaxRetries,
            MinAcquisitionPullTime = minPullTime,
            MaxAcquisitionPullTime = maxPullTime,
            TimeZone = facility.TimeZone
        }, cancellationToken);

        var lagDuration = BuildLagDuration(request.LagDays, request.LagHours, request.LagMinutes);
        await _queryDispatchGateway.SetLagDurationAsync(facilityId, lagDuration, cancellationToken);

        return new FhirServerInfoResponse
        {
            FhirServerBaseUrl = request.FhirServerBaseUrl,
            MaxConcurrentRequests = request.MaxConcurrentRequests,
            MaxRetries = request.MaxRetries,
            MinAcquisitionPullTime = request.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = request.MaxAcquisitionPullTime,
            LagDays = request.LagDays,
            LagHours = request.LagHours,
            LagMinutes = request.LagMinutes
        };
    }

    public Task<ConnectionResult> TestFhirConnectionAsync(string fhirServerBaseUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(fhirServerBaseUrl, UriKind.Absolute, out var parsedBaseUrl) ||
            (parsedBaseUrl.Scheme != Uri.UriSchemeHttp && parsedBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            return Task.FromResult(new ConnectionResult { Success = false, MessageKey = "fhirServerInfo.messages.invalidBaseUrl" });
        }

        return Task.FromResult(new ConnectionResult { Success = true, MessageKey = "fhirServerInfo.messages.testSuccess" });
    }

    internal static (int Days, int Hours, int Minutes) ParseLagDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return (0, 0, 0);
        }

        try
        {
            var timeSpan = XmlConvert.ToTimeSpan(duration);
            return (timeSpan.Days, timeSpan.Hours, timeSpan.Minutes);
        }
        catch (FormatException)
        {
            return (0, 0, 0);
        }
    }

    private static string BuildLagDuration(int days, int hours, int minutes) =>
        XmlConvert.ToString(new TimeSpan(days, hours, minutes, 0));

    private static TimeSpan ParsePullTime(string value, string fieldName)
    {
        if (!TimeSpan.TryParseExact(value, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
        {
            throw new InvalidOperationException($"{fieldName} must be a valid 24-hour time in HH:MM format.");
        }

        return parsed;
    }

    private static string? FormatPullTime(TimeSpan? value) =>
        value?.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
}
