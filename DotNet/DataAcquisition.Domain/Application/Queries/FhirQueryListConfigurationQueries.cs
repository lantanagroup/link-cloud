using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

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
        var result = await (from fl in _database.FhirListConfigurations
                           where fl.FacilityId == facilityId
                         select FhirListConfigurationModel.FromDomain(fl)).SingleOrDefaultAsync();

        return result;
    }

    public async Task<AuthenticationConfigurationModel?> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var result = await (from fl in _database.FhirListConfigurations
                            where fl.FacilityId == facilityId
                            select AuthenticationConfigurationModel.FromDomain(fl.Authentication)).SingleOrDefaultAsync();

        return result;
    }
}