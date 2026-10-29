using DataAcquisition.Domain.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;

public interface IAuth
{
    Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards any cached credential for the facility so the next request authenticates afresh.
    /// </summary>
    Task InvalidateAuthentication(string facilityId, CancellationToken cancellationToken = default);
}

