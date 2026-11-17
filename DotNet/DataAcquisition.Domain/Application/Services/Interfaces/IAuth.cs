using DataAcquisition.Domain.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;

public interface IAuth
{
    Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the facility's cached credential so the next request authenticates afresh, unless it was obtained
    /// at or after <paramref name="requestStartedUtc"/>, the start of the request that was refused.
    /// </summary>
    Task InvalidateAuthentication(string facilityId,
                                  DateTimeOffset requestStartedUtc,
                                  CancellationToken cancellationToken = default);
}

