using LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Managers;

public interface IFacilityLocationLocalCodeMappingManager
{
    Task<FacilityLocationLocalCodeMappingModel> Create(string facilityId, FacilityLocationLocalCodeMappingPostModel model, CancellationToken cancellationToken = default);
    Task<FacilityLocationLocalCodeMappingModel?> Update(string id, FacilityLocationLocalCodeMappingPutModel model, CancellationToken cancellationToken = default);
    Task Delete(string id, CancellationToken cancellationToken = default);
    Task DeleteForFacility(string facilityId, CancellationToken cancellationToken = default);
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
        FacilityLocationLocalCodeMappingPostModel model,
        CancellationToken cancellationToken = default)
    {
        var facilityLocation = await _dbContext.FacilityLocations.SingleOrDefaultAsync(location =>
            location.FacilityId == facilityId && location.LocationId == model.LocationId, cancellationToken)
            ?? throw new KeyNotFoundException("The requested facility location does not exist.");

        await ValidateHSLOCAsync(model.HSLOCId, cancellationToken);
        await EnsureMappingIsUniqueAsync(facilityLocation.Id, model.LocalCodeSystem, model.LocalCode, cancellationToken: cancellationToken);

        var mapping = new FacilityLocationLocalCodeMapping
        {
            FacilityLocationId = facilityLocation.Id,
            LocalCodeSystem = model.LocalCodeSystem,
            LocalCode = model.LocalCode,
            HSLOCId = model.HSLOCId,
            CreateDate = DateTime.UtcNow
        };

        _dbContext.FacilityLocationLocalCodeMappings.Add(mapping);
        await SaveChangesAsync(cancellationToken);

        return (await _mappingQueries.Get(mapping.Id, cancellationToken))!;
    }

    public async Task<FacilityLocationLocalCodeMappingModel?> Update(
        string id,
        FacilityLocationLocalCodeMappingPutModel model,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.FacilityLocationLocalCodeMappings.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (mapping == null)
        {
            return null;
        }

        await ValidateHSLOCAsync(model.HSLOCId, cancellationToken);
        await EnsureMappingIsUniqueAsync(mapping.FacilityLocationId, model.LocalCodeSystem, model.LocalCode, mapping.Id, cancellationToken);

        mapping.LocalCodeSystem = model.LocalCodeSystem;
        mapping.LocalCode = model.LocalCode;
        mapping.HSLOCId = model.HSLOCId;
        mapping.ModifyDate = DateTime.UtcNow;

        await SaveChangesAsync(cancellationToken);
        return await _mappingQueries.Get(mapping.Id, cancellationToken);
    }

    public async Task Delete(string id, CancellationToken cancellationToken = default)
    {
        await _dbContext.FacilityLocationLocalCodeMappings
            .Where(mapping => mapping.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteForFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        var facilityLocationIds = _dbContext.FacilityLocations
            .Where(location => location.FacilityId == facilityId)
            .Select(location => location.Id);

        await _dbContext.FacilityLocationLocalCodeMappings
            .Where(mapping => facilityLocationIds.Contains(mapping.FacilityLocationId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ValidateHSLOCAsync(Guid? hslocId, CancellationToken cancellationToken)
    {
        if (hslocId.HasValue && !await _dbContext.HSLOCS.AnyAsync(hsloc => hsloc.Id == hslocId.Value, cancellationToken))
        {
            throw new ArgumentException("The requested HSLOC does not exist.", nameof(hslocId));
        }
    }

    private async Task EnsureMappingIsUniqueAsync(
        string facilityLocationId,
        string localCodeSystem,
        string localCode,
        string? excludedMappingId = null,
        CancellationToken cancellationToken = default)
    {
        var duplicateExists = await _dbContext.FacilityLocationLocalCodeMappings.AnyAsync(mapping =>
            mapping.FacilityLocationId == facilityLocationId &&
            mapping.LocalCodeSystem == localCodeSystem &&
            mapping.LocalCode == localCode &&
            mapping.Id != excludedMappingId, cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("A mapping already exists for this facility location and local code.");
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new InvalidOperationException(
                "A mapping already exists for this facility location and local code.",
                exception);
        }
    }
}