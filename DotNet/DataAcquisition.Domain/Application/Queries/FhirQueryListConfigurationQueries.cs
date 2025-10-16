using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using System.Data.Entity;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryListConfigurationQueries
{
    Task<FhirListConfigurationModel?> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<AuthenticationConfigurationModel?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirQueryListConfigurationQueries : IFhirQueryListConfigurationQueries
{
    private readonly DataAcquisitionDbContext _database;

    public FhirQueryListConfigurationQueries(DataAcquisitionDbContext database)
    {
        _database = database;
    }

    public async Task<FhirListConfigurationModel?> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var result = await _database.FhirListConfigurations.Include(l => l.Authentication).FirstOrDefaultAsync(x => x.FacilityId == facilityId);
        return FhirListConfigurationModel.FromDomain(result);
    }

    public async Task<AuthenticationConfigurationModel?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var queryResult = await GetByFacilityIdAsync(facilityId, cancellationToken);

        if (queryResult == null)
        {
            throw new MissingFacilityConfigurationException(
                $"No configuration found for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        if (queryResult.Authentication == null)
        {
            throw new NotFoundException(
                $"No Authentication found on configuration for facilityId: {facilityId}. Unable to retrieve Authentication settings.");
        }

        return queryResult.Authentication;
    }
}