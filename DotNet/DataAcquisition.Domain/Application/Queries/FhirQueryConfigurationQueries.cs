using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;


public interface IFhirQueryConfigurationQueries
{
    Task<AuthenticationConfigurationModel?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default);
    Task<FhirQueryConfigurationModel?> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class FhirQueryConfigurationQueries : IFhirQueryConfigurationQueries
{
    private readonly DataAcquisitionDbContext _database;

    public FhirQueryConfigurationQueries(DataAcquisitionDbContext database)
    {
        _database = database;
    }

    public async Task<AuthenticationConfigurationModel?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var result = await (from fl in _database.FhirQueryConfigurations
                            where fl.FacilityId == facilityId
                            select AuthenticationConfigurationModel.FromDomain(fl.Authentication)).SingleOrDefaultAsync();

        return result;
    }

    public async Task<FhirQueryConfigurationModel?> GetByFacilityIdAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var result = await (from fl in _database.FhirQueryConfigurations
                            where fl.FacilityId == facilityId
                            select FhirQueryConfigurationModel.FromDomain(fl)).SingleOrDefaultAsync();

        return result;
    }
}