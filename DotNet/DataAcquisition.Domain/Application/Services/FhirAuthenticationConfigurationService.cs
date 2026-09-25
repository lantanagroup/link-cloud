using Azure;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

/// <summary>
/// Stores and reads the generic OAuth client-credentials configuration for a facility whose EHR
/// vendor is "Other". Secret values go to the secret manager; the configuration row keeps only the
/// secret names, which is what the OAuth handler resolves at acquisition time.
/// </summary>
public class FhirAuthenticationConfigurationService : IFhirAuthenticationConfigurationService
{
    private const string SecretNamePrefix = "fhir-oauth-";
    private const string ClientIdSuffix = "-client-id";
    private const string ClientSecretSuffix = "-client-secret";

    // Key Vault allows [0-9a-zA-Z-]{1,127}. The prefix, the 8-character hash, its separator and the
    // longest suffix take 34, so the slug gets what is left.
    private const int MaxSlugLength = 93;

    private readonly IFhirQueryConfigurationManager _fhirQueryConfigurationManager;
    private readonly IFhirQueryConfigurationQueries _fhirQueryConfigurationQueries;
    private readonly ISecretManager _secretManager;
    private readonly ICacheService _cacheService;

    private readonly ILogger<FhirAuthenticationConfigurationService> _logger;

    public FhirAuthenticationConfigurationService(IFhirQueryConfigurationManager fhirQueryConfigurationManager,
                                                  IFhirQueryConfigurationQueries fhirQueryConfigurationQueries,
                                                  ISecretManager secretManager,
                                                  ICacheService cacheService,
                                                  ILogger<FhirAuthenticationConfigurationService> logger)
    {
        _fhirQueryConfigurationManager = fhirQueryConfigurationManager
            ?? throw new ArgumentNullException(nameof(fhirQueryConfigurationManager));
        _fhirQueryConfigurationQueries = fhirQueryConfigurationQueries
            ?? throw new ArgumentNullException(nameof(fhirQueryConfigurationQueries));
        _secretManager = secretManager ?? throw new ArgumentNullException(nameof(secretManager));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<FhirAuthenticationConfigurationResponse?> GetAsync(string facilityId, CancellationToken ct)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirAuthenticationConfigurationService.Get");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new BadRequestException("FacilityId is required.");
        }

        var stored = (await _fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityId, ct))?.Authentication;

        if (!IsGenericOAuth(stored))
        {
            return null;
        }

        return new FhirAuthenticationConfigurationResponse
        {
            TokenUrl = stored!.TokenUrl ?? "",
            ClientId = await ReadSecretAsync(stored.ClientId, ct),
            Scope = stored.Scope ?? "",
            ClientSecretStored = !string.IsNullOrWhiteSpace(await ReadSecretAsync(stored.ClientSecret, ct))
        };
    }

    /// <inheritdoc />
    public async Task<FhirAuthenticationConfigurationResponse> CreateOrUpdateAsync(string facilityId,
                                                                                   FhirAuthenticationConfigurationRequest request,
                                                                                   CancellationToken ct)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirAuthenticationConfigurationService.CreateOrUpdate");
        activity?.SetTag(DiagnosticNames.FacilityId, facilityId);

        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new BadRequestException("FacilityId is required.");
        }

        if (request == null)
        {
            throw new BadRequestException("A configuration is required.");
        }

        // The row has to exist before anything else is decided: the manager's own check fires too late,
        // after the secrets are written. See docs/other-vendor-oauth-configuration.md.
        var queryConfiguration = await _fhirQueryConfigurationQueries.GetByFacilityIdAsync(facilityId, ct);

        if (queryConfiguration is null)
        {
            throw new NotFoundException("No FHIR query configuration exists for this facility.");
        }

        var stored = queryConfiguration.Authentication;
        var storedSecretName = IsGenericOAuth(stored) ? stored!.ClientSecret : null;
        var replacingSecret = !string.IsNullOrWhiteSpace(request.ClientSecret);

        if (!replacingSecret && string.IsNullOrWhiteSpace(await ReadSecretAsync(storedSecretName, ct)))
        {
            throw new BadRequestException("ClientSecret is required because no client secret is stored for this facility.");
        }

        var clientIdName = BuildSecretName(facilityId, ClientIdSuffix);

        // An operator may have provisioned the existing secret under a name of their own through the
        // older authentication endpoint. Keep writing to that name rather than orphaning it.
        var clientSecretName = replacingSecret
            ? BuildSecretName(facilityId, ClientSecretSuffix)
            : storedSecretName!;

        // Secrets before the row, so the row never points at a name that was never written. The window
        // this leaves on a repeat write is deliberate. See docs/other-vendor-oauth-configuration.md.
        await WriteSecretAsync(clientIdName, request.ClientId.Trim(), ct);

        if (replacingSecret)
        {
            await WriteSecretAsync(clientSecretName, request.ClientSecret!.Trim(), ct);
        }

        var configuration = new AuthenticationConfiguration
        {
            AuthType = nameof(AuthType.OAuth),
            TokenUrl = request.TokenUrl.Trim(),
            Scope = request.Scope.Trim(),
            ClientId = clientIdName,
            ClientSecret = clientSecretName
        };

        await _fhirQueryConfigurationManager.UpdateAuthenticationConfiguration(facilityId, configuration, ct);

        // The OAuth handler caches the access token under the bare facility id, so without this a
        // replaced client would keep working on the old token until it expired.
        await _cacheService.RemoveAsync(facilityId, ct);

        _logger.LogInformation("Replaced the FHIR authentication configuration for facility {FacilityId}.",
                               facilityId.SanitizeForLog());

        return new FhirAuthenticationConfigurationResponse
        {
            TokenUrl = configuration.TokenUrl,
            ClientId = request.ClientId.Trim(),
            Scope = configuration.Scope,
            ClientSecretStored = true
        };
    }

    /// <summary>
    /// True when the stored configuration is the generic OAuth kind this endpoint owns. A facility on
    /// Epic, Basic or custom headers is not one of ours, and neither is a facility with no
    /// authentication at all.
    /// </summary>
    private static bool IsGenericOAuth(AuthenticationConfigurationModel? stored)
    {
        return stored is not null &&
               string.Equals(stored.AuthType, nameof(AuthType.OAuth), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds a Key Vault-safe, deterministic name for one of the facility's secrets. The hash keeps
    /// facility ids that differ only in characters the slug replaces, such as "a.b" and "a_b", apart.
    /// </summary>
    private static string BuildSecretName(string facilityId, string suffix)
    {
        var slug = new string(facilityId
            .ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')
            .ToArray());

        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength];
        }

        var hash = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facilityId)))[..8]
            .ToLowerInvariant();

        return $"{SecretNamePrefix}{slug}-{hash}{suffix}";
    }

    private async Task WriteSecretAsync(string secretName, string secretValue, CancellationToken ct)
    {
        if (!await _secretManager.SetSecretAsync(secretName, secretValue, ct))
        {
            throw new InvalidOperationException($"The secret manager did not store the secret '{secretName}'.");
        }
    }

    /// <summary>
    /// Reads a secret by name, treating "no such secret" as absent rather than as a failure. Any other
    /// fault, such as a permission problem or an unreachable vault, is left to propagate.
    /// </summary>
    private async Task<string?> ReadSecretAsync(string? secretName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return null;
        }

        try
        {
            return await _secretManager.GetSecretAsync(secretName, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.NotFound)
        {
            _logger.LogDebug("No secret named {SecretName} is stored.", secretName.SanitizeForLog());
            return null;
        }
    }

    private static class StatusCodes
    {
        public const int NotFound = 404;
    }
}
