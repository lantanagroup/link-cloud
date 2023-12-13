﻿using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IFhirQueryConfigurationRepository : IMongoDbRepository<FhirQueryConfiguration>
{
    Task<AuthenticationConfiguration> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default);
    Task SaveAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default);
    Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default);

}
