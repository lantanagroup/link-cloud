using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.FacilityLocationMappings;
using LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Normalization.Domain.Managers;

public interface IFacilityLocationLocalCodeMappingManager
{
    Task<FacilityLocationLocalCodeMappingModel> Create(string facilityId, FacilityLocationLocalCodeMappingPostModel model, CancellationToken cancellationToken = default);
    Task<FacilityLocationLocalCodeMappingModel?> Update(string id, FacilityLocationLocalCodeMappingPutModel model, CancellationToken cancellationToken = default);
    Task Delete(string id, CancellationToken cancellationToken = default);
    Task DeleteForFacility(string facilityId, CancellationToken cancellationToken = default);
    Task UpdateFacilityLocationLocalCodeMappings(string facilityId, IReadOnlyList<HSLOCMappingResult> hslocMappingResults, CancellationToken cancellationToken = default);
}

public class FacilityLocationLocalCodeMappingManager : IFacilityLocationLocalCodeMappingManager
{
    private readonly NormalizationDbContext _dbContext;
    private readonly IFacilityLocationLocalCodeMappingQueries _mappingQueries;
    private readonly IFacilityLocationManager _facilityLocationManager;
    private readonly IHSLOCQueries _hslocQueries;
    private readonly ILogger<FacilityLocationLocalCodeMappingManager> _logger;

    public FacilityLocationLocalCodeMappingManager(
        NormalizationDbContext dbContext,
        IFacilityLocationManager facilityLocationManager,
        IFacilityLocationLocalCodeMappingQueries mappingQueries,
        IHSLOCQueries hslocQueries,
        ILogger<FacilityLocationLocalCodeMappingManager> logger)
    {
        _dbContext = dbContext;
        _mappingQueries = mappingQueries;
        _facilityLocationManager = facilityLocationManager;
        _hslocQueries = hslocQueries;
        _logger = logger;
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

    public async Task UpdateFacilityLocationLocalCodeMappings(string facilityId, IReadOnlyList<HSLOCMappingResult> hslocMappingResults, CancellationToken cancellationToken = default)
    {
        var hslocCodes = await _hslocQueries.GetAll(true, cancellationToken);
        var existingLocationsAndMappings = await _dbContext.FacilityLocations.AsNoTracking().Include(i => i.FacilityLocationLocalCodeMappings).Where(q => q.FacilityId == facilityId).ToListAsync(cancellationToken);
        foreach(var hslocMappingResult in hslocMappingResults)
        {
            //add or update the FacilityLocation
            var existingLocation = existingLocationsAndMappings.FirstOrDefault(f => f.LocationId == hslocMappingResult.Location.Id);
            var facilityLocationId = existingLocation?.Id;
            if(existingLocation == null)
            {
                //never seen this location before, create it
                //it is possible for parallel threads to attempt to create the same facility location, so we need to handle the case where a duplicate insert is attempted
                try
                {
                    facilityLocationId = (await _facilityLocationManager.Create(facilityId, new FacilityLocationPostModel
                    {
                        LocationId = hslocMappingResult.Location.Id,
                        LocationName = hslocMappingResult.Location.Name,
                        LocationAlias = string.Join(", ", hslocMappingResult.Location.Alias),
                        PartOfId = hslocMappingResult.Location.PartOf?.Reference.SplitReference()
                    }, cancellationToken)).Id;
                }
                catch (InvalidOperationException exception) when (
                    exception.Message == "A facility location with the supplied location identifier already exists." &&
                    (exception.InnerException == null ||
                     exception.InnerException is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }))
                {
                    if (exception.InnerException is DbUpdateException updateException)
                    {
                        foreach (var entry in updateException.Entries)
                        {
                            if (entry.Entity is FacilityLocation && entry.State == EntityState.Added)
                            {
                                entry.State = EntityState.Detached; //prevent further attempts to add this entity
                            }
                        }
                    }

                    _logger.LogDebug(exception,
                        "Ignoring duplicate facility location insert for FacilityId={FacilityId}, LocationId={LocationId}.",
                        facilityId.SanitizeForLog(), hslocMappingResult.Location.Id.SanitizeForLog());
                    continue;
                }
            }
            else if(HasLocationChanged(existingLocation, hslocMappingResult.Location))
            {
                //location has changed since it was last seen, update it
                await _facilityLocationManager.Update(  facilityId, 
                                                        existingLocation.LocationId, 
                                                        hslocMappingResult.Location.Name, 
                                                        string.Join(", ", hslocMappingResult.Location.Alias), 
                                                        hslocMappingResult.Location.PartOf?.Reference.SplitReference(), 
                                                        cancellationToken);
            }

            //add or update the FacilityLocationLocalCodeMapping
            foreach(var locationTypeCode in hslocMappingResult.LocationTypeCodes)
            {
                var existingMapping = existingLocation?.FacilityLocationLocalCodeMappings?.FirstOrDefault(m => m.LocalCodeSystem == locationTypeCode.SourceSystem && m.LocalCode == locationTypeCode.SourceCode);
                if(existingMapping == null)
                {
                    //never seen this location type code before, create it
                    //it is possible for parallel threads to attempt to create the same facility location mapping, so we need to handle the case where a duplicate insert is attempted
                    try
                    {
                        var hslocId = findHSLOCId(hslocCodes, locationTypeCode);
                        await Create(facilityId, new FacilityLocationLocalCodeMappingPostModel
                        {
                            LocationId = hslocMappingResult.Location.Id,
                            LocalCodeSystem = locationTypeCode.SourceSystem ?? string.Empty,
                            LocalCode = locationTypeCode.SourceCode ?? string.Empty,
                            HSLOCId = hslocId
                        }, cancellationToken);
                    }
                    catch (InvalidOperationException exception) when (
                        exception.Message == "A mapping already exists for this facility location and local code." &&
                        (exception.InnerException == null ||
                         exception.InnerException is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }))
                    {
                        if (exception.InnerException is DbUpdateException updateException)
                        {
                            foreach (var entry in updateException.Entries)
                            {
                                if (entry.Entity is FacilityLocationLocalCodeMapping && entry.State == EntityState.Added)
                                {
                                    entry.State = EntityState.Detached; //prevent further attempts to add this entity
                                }
                            }
                        }

                        _logger.LogDebug(exception,
                            "Ignoring duplicate facility location mapping insert for FacilityId={FacilityId}, LocationId={LocationId}, LocalCodeSystem={LocalCodeSystem}, LocalCode={LocalCode}.",
                            facilityId.SanitizeForLog(), hslocMappingResult.Location.Id.SanitizeForLog(),
                            locationTypeCode.SourceSystem.SanitizeForLog(), locationTypeCode.SourceCode.SanitizeForLog());
                        continue;
                    }
                }
                else
                {
                    var hslocId = findHSLOCId(hslocCodes, locationTypeCode);
                    if(HasMappingChanged(existingMapping, locationTypeCode, hslocId))
                    {
                        //mapping has changed since it was last seen, update it
                        await Update(existingMapping.Id, new FacilityLocationLocalCodeMappingPutModel
                        {
                            LocalCodeSystem = locationTypeCode.SourceSystem ?? string.Empty,
                            LocalCode = locationTypeCode.SourceCode ?? string.Empty,
                            HSLOCId = hslocId
                        }, cancellationToken);
                    }
                }
            }

            //delete any mappings that are no longer present
            var mappingsToDelete = existingLocation?.FacilityLocationLocalCodeMappings?.Where(m => !hslocMappingResult.LocationTypeCodes.Any(l => l.SourceSystem == m.LocalCodeSystem && l.SourceCode == m.LocalCode)).ToList();
            if(mappingsToDelete != null)
            {
                foreach(var mappingToDelete in mappingsToDelete)
                {
                    await Delete(mappingToDelete.Id, cancellationToken);
                }
            }
        }
    }

    private Guid? findHSLOCId(List<HSLOC> hslocCodes, HSLOCMappingResultCode locationTypeCode)
    {
        //If the source system was already HSLOC, use the source code to find the HSLOC id, otherwise use the target code.
        if(MappingTargetSystems.IsHsloc(locationTypeCode.SourceSystem))
        {
            var hslocId = hslocCodes.FirstOrDefault(h => h.HSLOCCode == locationTypeCode.SourceCode)?.Id;
            if(hslocId == null)
            {
                _logger.LogWarning("Mapping system {TargetSystem} is HSLOC, but the target code {TargetCode} does not match any known HSLOC codes.", locationTypeCode.SourceSystem.SanitizeForLog(), locationTypeCode.SourceCode.SanitizeForLog());
            }
            return hslocId;
        }
        return hslocCodes.FirstOrDefault(h => h.HSLOCCode == locationTypeCode.TargetCode)?.Id;
    }

    protected bool HasLocationChanged(FacilityLocation existing, Location incoming)
    {
        return !(existing.LocationId == incoming.Id &&
               existing.LocationName == incoming.Name &&
               existing.LocationAlias == string.Join(", ", incoming.Alias) &&
               existing.PartOfId == incoming.PartOf?.Reference.SplitReference());
    }

    protected bool HasMappingChanged(FacilityLocationLocalCodeMapping existing, HSLOCMappingResultCode incoming, Guid? hslocId)
    {
        return !(existing.LocalCodeSystem == incoming.SourceSystem &&
               existing.LocalCode == incoming.SourceCode &&
               existing.HSLOCId == hslocId);
    }
}