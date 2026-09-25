using System;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;

/// <summary>
/// Service for managing the stored configuration for a facility whose EHR vendor is "Other".
/// </summary>
public interface IFhirAuthenticationConfigurationService
{
    /// <summary>
    /// Gets the facility's generic OAuth configuration.
    /// </summary>
    /// <param name="facilityId">Facility ID to get.</param>
    /// <param name="ct">Token to signal cancellation.</param>
    /// <returns>
    /// The configuration, or null when the facility has none: no configuration row, no authentication
    /// on it, or another authentication type. A configuration whose client secret no longer resolves
    /// is still returned, with <c>ClientSecretStored</c> reporting that it is missing.
    /// </returns>
    Task<FhirAuthenticationConfigurationResponse?> GetAsync(string facilityId, CancellationToken ct);

    /// <summary>
    /// Creates or replaces the facility's generic OAuth configuration.
    /// </summary>
    /// <param name="facilityId">Facility ID to store against.</param>
    /// <param name="request">The configuration to store.</param>
    /// <param name="ct">Token to signal cancellation.</param>
    /// <returns>The stored configuration.</returns>
    /// <exception cref="Models.Exceptions.NotFoundException">
    /// The facility has no FHIR query configuration to attach authentication to.
    /// </exception>
    /// <exception cref="Models.Exceptions.BadRequestException">
    /// The facility id is missing, or no client secret was supplied and none is stored.
    /// </exception>
    Task<FhirAuthenticationConfigurationResponse> CreateOrUpdateAsync(string facilityId,
                                                                      FhirAuthenticationConfigurationRequest request,
                                                                      CancellationToken ct);
}
