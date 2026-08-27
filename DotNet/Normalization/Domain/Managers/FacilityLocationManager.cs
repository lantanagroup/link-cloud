using LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Managers;

public interface IFacilityLocationManager
{
    Task<FacilityLocationModel?> Get(string facilityId, string locationId);
    Task<FacilityLocationModel> Create(string facilityId, FacilityLocationPostModel model);
}

public class FacilityLocationManager : IFacilityLocationManager
{
    private readonly NormalizationDbContext _dbContext;

    public FacilityLocationManager(NormalizationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FacilityLocationModel?> Get(string facilityId, string locationId)
    {
        var facilityLocation = await _dbContext.FacilityLocations.SingleOrDefaultAsync(location =>
            location.FacilityId == facilityId && location.LocationId == locationId);

        return facilityLocation == null ? null : ToModel(facilityLocation);
    }

    public async Task<FacilityLocationModel> Create(string facilityId, FacilityLocationPostModel model)
    {
        if (await _dbContext.FacilityLocations.AnyAsync(location =>
                location.FacilityId == facilityId && location.LocationId == model.LocationId))
        {
            throw new InvalidOperationException("A facility location with the supplied location identifier already exists.");
        }

        var parentFacilityLocationId = await ResolveParentFacilityLocationId(facilityId, model.PartOfId);
        var facilityLocation = new FacilityLocation
        {
            FacilityId = facilityId,
            LocationId = model.LocationId,
            PartOfId = model.PartOfId,
            ParentFacilityLocationId = parentFacilityLocationId,
            LocationName = model.LocationName,
            LocationAlias = model.LocationAlias,
            CreateDate = DateTime.UtcNow
        };

        _dbContext.FacilityLocations.Add(facilityLocation);
        await _dbContext.SaveChangesAsync();

        return ToModel(facilityLocation);
    }

    private async Task<string?> ResolveParentFacilityLocationId(string facilityId, string? partOfId)
    {
        if (string.IsNullOrWhiteSpace(partOfId))
        {
            return null;
        }

        return await _dbContext.FacilityLocations
            .Where(location => location.FacilityId == facilityId && location.LocationId == partOfId)
            .Select(location => location.Id)
            .SingleOrDefaultAsync();
    }

    private static FacilityLocationModel ToModel(FacilityLocation facilityLocation) => new()
    {
        Id = facilityLocation.Id,
        FacilityId = facilityLocation.FacilityId,
        LocationId = facilityLocation.LocationId,
        PartOfId = facilityLocation.PartOfId,
        LocationName = facilityLocation.LocationName,
        LocationAlias = facilityLocation.LocationAlias,
        CreateDate = facilityLocation.CreateDate,
        ModifyDate = facilityLocation.ModifyDate
    };
}