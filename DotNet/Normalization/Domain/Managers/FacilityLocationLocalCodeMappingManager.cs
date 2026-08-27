using LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Managers;

public interface IFacilityLocationLocalCodeMappingManager
{
    Task<FacilityLocationLocalCodeMappingModel> Create(string facilityId, FacilityLocationLocalCodeMappingPostModel model);
    Task<FacilityLocationLocalCodeMappingModel?> Update(string id, FacilityLocationLocalCodeMappingPutModel model);
    Task Delete(string id);
    Task DeleteForFacility(string facilityId);
}

public class FacilityLocationLocalCodeMappingManager : IFacilityLocationLocalCodeMappingManager
{
    private readonly NormalizationDbContext _dbContext;
    private readonly IFacilityLocationLocalCodeMappingQueries _mappingQueries;

    public FacilityLocationLocalCodeMappingManager(
        NormalizationDbContext dbContext,
        IFacilityLocationLocalCodeMappingQueries mappingQueries)
    {
        _dbContext = dbContext;
        _mappingQueries = mappingQueries;
    }

    public async Task<FacilityLocationLocalCodeMappingModel> Create(
        string facilityId,
        FacilityLocationLocalCodeMappingPostModel model)
    {
        var facilityLocation = await _dbContext.FacilityLocations.SingleOrDefaultAsync(location =>
            location.FacilityId == facilityId && location.LocationId == model.LocationId)
            ?? throw new KeyNotFoundException("The requested facility location does not exist.");

        await ValidateHSLOCAsync(model.HSLOCId);
        await EnsureMappingIsUniqueAsync(facilityLocation.Id, model.LocalCodeSystem, model.LocalCode);

        var mapping = new FacilityLocationLocalCodeMapping
        {
            FacilityLocationId = facilityLocation.Id,
            LocalCodeSystem = model.LocalCodeSystem,
            LocalCode = model.LocalCode,
            HSLOCId = model.HSLOCId,
            CreateDate = DateTime.UtcNow
        };

        _dbContext.FacilityLocationLocalCodeMappings.Add(mapping);
        await _dbContext.SaveChangesAsync();

        return (await _mappingQueries.Get(mapping.Id))!;
    }

    public async Task<FacilityLocationLocalCodeMappingModel?> Update(
        string id,
        FacilityLocationLocalCodeMappingPutModel model)
    {
        var mapping = await _dbContext.FacilityLocationLocalCodeMappings.SingleOrDefaultAsync(candidate => candidate.Id == id);
        if (mapping == null)
        {
            return null;
        }

        await ValidateHSLOCAsync(model.HSLOCId);
        await EnsureMappingIsUniqueAsync(mapping.FacilityLocationId, model.LocalCodeSystem, model.LocalCode, mapping.Id);

        mapping.LocalCodeSystem = model.LocalCodeSystem;
        mapping.LocalCode = model.LocalCode;
        mapping.HSLOCId = model.HSLOCId;
        mapping.ModifyDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return await _mappingQueries.Get(mapping.Id);
    }

    public async Task Delete(string id)
    {
        await _dbContext.FacilityLocationLocalCodeMappings
            .Where(mapping => mapping.Id == id)
            .ExecuteDeleteAsync();
    }

    public async Task DeleteForFacility(string facilityId)
    {
        var facilityLocationIds = _dbContext.FacilityLocations
            .Where(location => location.FacilityId == facilityId)
            .Select(location => location.Id);

        await _dbContext.FacilityLocationLocalCodeMappings
            .Where(mapping => facilityLocationIds.Contains(mapping.FacilityLocationId))
            .ExecuteDeleteAsync();
    }

    private async Task ValidateHSLOCAsync(Guid? hslocId)
    {
        if (hslocId.HasValue && !await _dbContext.HSLOCS.AnyAsync(hsloc => hsloc.Id == hslocId.Value))
        {
            throw new ArgumentException("The requested HSLOC does not exist.", nameof(hslocId));
        }
    }

    private async Task EnsureMappingIsUniqueAsync(
        string facilityLocationId,
        string localCodeSystem,
        string localCode,
        string? excludedMappingId = null)
    {
        var duplicateExists = await _dbContext.FacilityLocationLocalCodeMappings.AnyAsync(mapping =>
            mapping.FacilityLocationId == facilityLocationId &&
            mapping.LocalCodeSystem == localCodeSystem &&
            mapping.LocalCode == localCode &&
            mapping.Id != excludedMappingId);

        if (duplicateExists)
        {
            throw new InvalidOperationException("A mapping already exists for this facility location and local code.");
        }
    }
}