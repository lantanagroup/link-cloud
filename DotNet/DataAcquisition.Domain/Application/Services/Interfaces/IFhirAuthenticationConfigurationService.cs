using System;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;

/// <summary>
/// Service for managing the stored configuration for a facility whose EHR vendor is "Other".
/// </summary>
public interface IFhirAuthenticationConfigurationService
{
    /// <summary>
    /// Gets the stored configuration for a facility whose EHR vendor is "Other". Throws an exception if the facility does not exist.
    /// </summary>
    /// <param name="facilityId">Facility ID to get</param>
    /// <param name="ct">Token to signal cancellation</param>
    /// <returns>Fhir configuration</returns>
    Task<FhirAuthenticationConfigurationResponse?> GetAsync(string facilityId, CancellationToken ct);

    /// <summary>
    /// Creates or updates the stored configuration for a facility whose EHR vendor is "Other". Throws an exception if the facility does not exist
    /// </summary>
    Task<FhirAuthenticationConfigurationResponse> CreateOrUpdateAsync(string facilityId, FhirAuthenticationConfigurationRequest request, CancellationToken ct);
}
