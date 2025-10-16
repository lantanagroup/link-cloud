using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;


public interface IFhirQueryConfigurationQueries
{
    Task<AuthenticationConfigurationModel?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirQueryConfigurationModel?> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirQueryConfigurationQueries : IFhirQueryConfigurationQueries
{
    private readonly ILogger<FhirQueryConfigurationQueries> _logger;
    private readonly IDatabase _database;

    public FhirQueryConfigurationQueries(IDatabase database, ILogger<FhirQueryConfigurationQueries> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database;
    }

    public async Task<AuthenticationConfigurationModel?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var queryResult = await _database.FhirQueryConfigurationRepository.SingleOrDefaultAsync(x => x.FacilityId == facilityId);

        if (queryResult == null)
        {
            throw new NotFoundException($"No configuration found for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        return AuthenticationConfigurationModel.FromDomain(queryResult.Authentication);
    }

    public async Task<FhirQueryConfigurationModel?> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var result = await _database.FhirQueryConfigurationRepository.FirstOrDefaultAsync(q => q.FacilityId == facilityId);
        return FhirQueryConfigurationModel.FromDomain(result);
    }
}