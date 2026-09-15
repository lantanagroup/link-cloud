using LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Managers;

public interface IFacilityLocationManager
{
    Task<FacilityLocationModel?> Get(string facilityId, string locationId, CancellationToken cancellationToken = default);
    Task<FacilityLocationModel> Create(string facilityId, FacilityLocationPostModel model, CancellationToken cancellationToken = default);
}

public class FacilityLocationManager : IFacilityLocationManager
{
    private readonly NormalizationDbContext _dbContext;

    public FacilityLocationManager(NormalizationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FacilityLocationModel?> Get(string facilityId, string locationId, CancellationToken cancellationToken = default)
    {
        var facilityLocation = await _dbContext.FacilityLocations.SingleOrDefaultAsync(location =>
            location.FacilityId == facilityId && location.LocationId == locationId,
            cancellationToken);

        return facilityLocation == null ? null : ToModel(facilityLocation);
    }

    public async Task<FacilityLocationModel> Create(
        string facilityId,
        FacilityLocationPostModel model,
        CancellationToken cancellationToken = default)
    {
        if (await _dbContext.FacilityLocations.AnyAsync(location =>
                location.FacilityId == facilityId && location.LocationId == model.LocationId,
                cancellationToken))
        {
            throw new InvalidOperationException("A facility location with the supplied location identifier already exists.");
        }

        var parentFacilityLocationId = await ResolveParentFacilityLocationId(facilityId, model.PartOfId, cancellationToken);
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
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _dbContext.FacilityLocations
                .Where(location => location.FacilityId == facilityId && location.PartOfId == facilityLocation.LocationId)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(location => location.ParentFacilityLocationId, facilityLocation.Id), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(
                "A facility location with the supplied location identifier already exists.",
                exception);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return ToModel(facilityLocation);
    }

    private async Task<string?> ResolveParentFacilityLocationId(
        string facilityId,
        string? partOfId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partOfId))
        {
            return null;
        }

        return await _dbContext.FacilityLocations
            .Where(location => location.FacilityId == facilityId && location.LocationId == partOfId)
            .Select(location => location.Id)
            .SingleOrDefaultAsync(cancellationToken);
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