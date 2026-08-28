using System.Text.Json;
using System.Xml;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.FacilityAdministration;

public class FacilityAdministrationService : IFacilityAdministrationService
{
    private const string LagDispatchEvent = "Discharge";

    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly IFacilityWriteLock _writeLock;
    private readonly IFacilityServiceClient _facilityServiceClient;
    private readonly IDataAcquisitionServiceClient _dataAcquisitionServiceClient;
    private readonly IDataAcquisitionRawClient _dataAcquisitionRawClient;
    private readonly IQueryDispatchServiceClient _queryDispatchServiceClient;

    public FacilityAdministrationService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IFacilityWriteLock writeLock,
        IFacilityServiceClient facilityServiceClient,
        IDataAcquisitionServiceClient dataAcquisitionServiceClient,
        IDataAcquisitionRawClient dataAcquisitionRawClient,
        IQueryDispatchServiceClient queryDispatchServiceClient)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _writeLock = writeLock;
        _facilityServiceClient = facilityServiceClient;
        _dataAcquisitionServiceClient = dataAcquisitionServiceClient;
        _dataAcquisitionRawClient = dataAcquisitionRawClient;
        _queryDispatchServiceClient = queryDispatchServiceClient;
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

        var fhirConfigResponse = await _dataAcquisitionServiceClient.GetFhirQueryConfigurationAsync(facilityId, cancellationToken);
        var fhirConfig = ParseFhirQueryConfiguration(fhirConfigResponse);

        var dispatchConfigResponse = await _queryDispatchServiceClient.GetConfigurationAsync(facilityId, cancellationToken);
        var (lagDays, lagHours, lagMinutes) = ParseLagDuration(FindLagSchedule(dispatchConfigResponse.IsSuccessStatusCode ? dispatchConfigResponse.Body : null));

        return new FhirServerInfoResponse
        {
            FhirServerBaseUrl = fhirConfig?.FhirServerBaseUrl,
            MaxConcurrentRequests = fhirConfig?.MaxConcurrentRequests,
            MaxRetries = fhirConfig?.MaxRetries,
            MinAcquisitionPullTime = FormatPullTime(fhirConfig?.MinAcquisitionPullTime),
            MaxAcquisitionPullTime = FormatPullTime(fhirConfig?.MaxAcquisitionPullTime),
            LagDays = lagDays,
            LagHours = lagHours,
            LagMinutes = lagMinutes
        };
    }

    public async Task<FhirServerInfoResponse?> UpdateFhirServerInfoAsync(UpdateFhirServerInfoRequest request, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsFacilityAdmin)
        {
            throw new InvalidOperationException("FACADMIN is required to update FHIR server information.");
        }

        var facilityId = _userContext.RequireFacilityId();

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

        await using var writeLock = await _writeLock.AcquireAsync(facilityId, cancellationToken);

        var facilityResponse = await _facilityServiceClient.GetAsync(facilityId, cancellationToken);
        if (facilityResponse.StatusCode == 404)
        {
            return null;
        }

        if (!facilityResponse.IsSuccessStatusCode || facilityResponse.Body is null)
        {
            throw new InvalidOperationException($"Unable to retrieve facility configuration from Tenant. Tenant returned HTTP {facilityResponse.StatusCode}.");
        }

        var timeZone = facilityResponse.Body.TimeZone;

        var existingResponse = await _dataAcquisitionServiceClient.GetFhirQueryConfigurationAsync(facilityId, cancellationToken);
        var existing = ParseFhirQueryConfiguration(existingResponse);
        var existingId = existing?.Id;

        if (existingId is null)
        {
            var createResponse = await _dataAcquisitionServiceClient.CreateFhirQueryConfigurationAsync(new CreateFhirQueryConfigurationRequestApiModel
            {
                FacilityId = facilityId,
                FhirServerBaseUrl = request.FhirServerBaseUrl,
                MaxConcurrentRequests = request.MaxConcurrentRequests,
                MaxRetries = request.MaxRetries,
                MinAcquisitionPullTime = minPullTime,
                MaxAcquisitionPullTime = maxPullTime,
                TimeZone = timeZone
            }, cancellationToken);

            if (!createResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Unable to save FHIR query configuration in Data Acquisition. Data Acquisition returned HTTP {createResponse.StatusCode}.");
            }
        }
        else
        {
            await _dataAcquisitionRawClient.UpdateFhirQueryConfigurationAsync(new UpdateFhirQueryConfigurationPayload
            {
                Id = existingId,
                FacilityId = facilityId,
                FhirServerBaseUrl = request.FhirServerBaseUrl,
                MaxConcurrentRequests = request.MaxConcurrentRequests,
                MaxRetries = request.MaxRetries,
                MinAcquisitionPullTime = minPullTime,
                MaxAcquisitionPullTime = maxPullTime,
                TimeZone = timeZone
            }, cancellationToken);
        }

        var lagDuration = BuildLagDuration(request.LagDays, request.LagHours, request.LagMinutes);
        var dispatchUpsertResponse = await _queryDispatchServiceClient.UpsertQueryDispatchConfigurationAsync(facilityId, new QueryDispatchConfigurationApiModel
        {
            FacilityId = facilityId,
            DispatchSchedules =
            [
                new DispatchScheduleApiModel { Event = LagDispatchEvent, Duration = lagDuration }
            ]
        }, cancellationToken);

        if (!dispatchUpsertResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Unable to save acquisition lag configuration in Query Dispatch. Query Dispatch returned HTTP {dispatchUpsertResponse.StatusCode}.");
        }

        await writeLock.CommitAsync(cancellationToken);

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

    private static readonly JsonSerializerOptions FhirQueryConfigurationJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static FhirQueryConfigurationDetail? ParseFhirQueryConfiguration(LinkApiResponse response)
    {
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(response.RawBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FhirQueryConfigurationDetail>(response.RawBody, FhirQueryConfigurationJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindLagSchedule(QueryDispatchConfigurationApiModel? configuration) =>
        configuration?.DispatchSchedules
            .FirstOrDefault(schedule => string.Equals(schedule.Event, LagDispatchEvent, StringComparison.OrdinalIgnoreCase))
            ?.Duration;

    private static (int Days, int Hours, int Minutes) ParseLagDuration(string? duration)
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
