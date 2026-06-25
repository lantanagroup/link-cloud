using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IEncounterMappingManager
{
    Task<EncounterMappingModel> CreateAsync(CreateEncounterMappingModel model);
    Task<EncounterMappingModel> UpdateByIdAsync(int id, UpdateEncounterMappingModel model);
    Task<EncounterMappingModel> UpdateByFacilityIdAndEncounterIdAsync(string facilityId, string encounterId, UpdateEncounterMappingModel model);
    Task<int> RecomputeMappedToOrgForLocationMappingAsync(int organizationLocationMappingId, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(int id);
    Task DeleteByFacilityIdAsync(string facilityId);
    Task DeleteByPatientIdAsync(string patientId);
    Task DeleteByEncounterIdAndFacilityIdAsync(string encounterId, string facilityId);
}

public class EncounterMappingManager : IEncounterMappingManager
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;

    public EncounterMappingManager(IDatabase database, DataAcquisitionDbContext dbContext)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<EncounterMappingModel> CreateAsync(CreateEncounterMappingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        ArgumentException.ThrowIfNullOrEmpty(model.FacilityId, nameof(model.FacilityId));
        ArgumentException.ThrowIfNullOrEmpty(model.EncounterId, nameof(model.EncounterId));
        ArgumentException.ThrowIfNullOrEmpty(model.PatientId, nameof(model.PatientId));

        var now = DateTime.UtcNow;
        var entity = new EncounterMapping
        {
            FacilityId = model.FacilityId,
            PatientId = model.PatientId,
            EncounterId = model.EncounterId,
            MappedToOrg = model.MappedToOrg,
            CreateDate = now,
            ModifiedDate = now
        };

        var existingMapping = await _database.EncounterMappingRepository.FirstOrDefaultAsync(m => 
            m.FacilityId == model.FacilityId && 
            m.EncounterId == model.EncounterId);
        
        if (existingMapping != null) {
            throw new EntityAlreadyExistsException($"An EncounterMapping already exists for FacilityId {model.FacilityId} and EncounterId {model.EncounterId}");
        }
        
        if (model.OrganizationLocationMappingIds != null)
        {
            foreach (var locId in model.OrganizationLocationMappingIds)
            {
                entity.EncounterLocations.Add(new EncounterLocation
                {
                    OrganizationLocationMappingId = locId,
                    CreateDate = now,
                    ModifiedDate = now
                });
            }
        }

        try
        {
            await _database.EncounterMappingRepository.AddAsync(entity);
            await _database.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The pre-check above is non-atomic: a concurrent request can insert the same
            // (FacilityId, EncounterId) between the check and SaveChanges, tripping the
            // unique constraint (UQ_EncounterMapping_FacilityId_EncounterId). Re-check so we
            // still surface a 409 in that race; if it isn't a duplicate, rethrow the original.
            var concurrent = await _database.EncounterMappingRepository.FirstOrDefaultAsync(m =>
                m.FacilityId == model.FacilityId &&
                m.EncounterId == model.EncounterId);

            if (concurrent != null)
            {
                throw new EntityAlreadyExistsException($"An EncounterMapping already exists for FacilityId {model.FacilityId} and EncounterId {model.EncounterId}");
            }

            // Not a duplicate. Check to see if it's a foreign key failure where the location id is invalid, if
            // so, return the invalid ids
            if (model.OrganizationLocationMappingIds is { Count: > 0 })
            {
                var requestedIds = model.OrganizationLocationMappingIds.Distinct().ToList();
                var existingIds = (await _database.LocationMappingRepository
                        .FindAsync(m => requestedIds.Contains(m.LocationMappingId)))
                    .Select(m => m.LocationMappingId)
                    .ToHashSet();

                var missingIds = requestedIds.Where(id => !existingIds.Contains(id)).ToList();
                if (missingIds.Count > 0)
                {
                    throw new BadRequestException($"Invalid OrganizationLocationMappingId(s): {string.Join(", ", missingIds)}");
                }
            }

            throw;
        }

        return ProjectToModel(entity);
    }

    public async Task<EncounterMappingModel> UpdateByIdAsync(int id, UpdateEncounterMappingModel model)
    {
        var entity = await _database.EncounterMappingRepository.GetAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"EncounterMapping with Id {id} not found.");

        if (model.MappedToOrg.HasValue)
            entity.MappedToOrg = model.MappedToOrg.Value;

        if (model.OrganizationLocationMappingIds != null)
        {
            // Simple sync: remove existing and add new
            var existingLocations = await _database.EncounterLocationRepository.FindAsync(l => l.EncounterMappingId == id);
            foreach (var loc in existingLocations)
            {
                _database.EncounterLocationRepository.Remove(loc);
            }
            await _database.SaveChangesAsync(); // Save removal before adding new to avoid identity issues or constraint conflicts if re-adding same IDs

            foreach (var locId in model.OrganizationLocationMappingIds)
            {
                entity.EncounterLocations.Add(new EncounterLocation
                {
                    OrganizationLocationMappingId = locId,
                    CreateDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                });
            }
        }

        entity.ModifiedDate = DateTime.UtcNow;
        _database.EncounterMappingRepository.Update(entity);
        await _database.SaveChangesAsync();

        return ProjectToModel(entity);
    }

    public async Task<EncounterMappingModel> UpdateByFacilityIdAndEncounterIdAsync(string facilityId, string encounterId, UpdateEncounterMappingModel model)
    {
        var entity = await _database.EncounterMappingRepository
            .FirstOrDefaultAsync(m => m.FacilityId == facilityId && m.EncounterId == encounterId);

        if (entity == null)
            throw new KeyNotFoundException($"No mapping found for FacilityId {facilityId} and EncounterId {encounterId}");

        return await UpdateByIdAsync(entity.EncounterMappingId, model);
    }

    public async Task<int> RecomputeMappedToOrgForLocationMappingAsync(
        int organizationLocationMappingId,
        CancellationToken cancellationToken = default)
    {
        // Recompute (not overwrite): an encounter is mapped to the org if ANY of its referenced
        // locations is an org location. Overwriting MappedToOrg with a single location's value would
        // let a trailing non-org location clear the flag for a multi-location encounter.
        var recomputed = await _dbContext.EncounterMappings
            .Where(mapping => mapping.EncounterLocations.Any(location =>
                location.OrganizationLocationMappingId == organizationLocationMappingId))
            .Select(mapping => new
            {
                mapping.EncounterMappingId,
                AnyOrg = mapping.EncounterLocations.Any(location => location.OrganizationLocationMapping.IsOrgLocation)
            })
            .ToListAsync(cancellationToken);

        if (recomputed.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        var total = 0;

        foreach (var group in recomputed.GroupBy(x => x.AnyOrg))
        {
            var ids = group.Select(x => x.EncounterMappingId).ToList();
            total += await _dbContext.EncounterMappings
                .Where(mapping => ids.Contains(mapping.EncounterMappingId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(mapping => mapping.MappedToOrg, group.Key)
                    .SetProperty(mapping => mapping.ModifiedDate, now),
                    cancellationToken);
        }

        return total;
    }

    public async Task DeleteByIdAsync(int id)
    {
        var entity = await _database.EncounterMappingRepository.GetAsync(id);
        if (entity != null)
        {
            // Related EncounterLocations should be handled by cascade delete if configured in EF, 
            // but let's be explicit if needed or rely on repository.
            // Based on EncounterLocation entity, it has a FK to EncounterMapping.
            _database.EncounterMappingRepository.Remove(entity);
            await _database.SaveChangesAsync();
        }
    }

    public async Task DeleteByFacilityIdAsync(string facilityId)
    {
        var entities = await _database.EncounterMappingRepository.FindAsync(m => m.FacilityId == facilityId);
        foreach (var entity in entities)
        {
            _database.EncounterMappingRepository.Remove(entity);
        }
        await _database.SaveChangesAsync();
    }

    public async Task DeleteByPatientIdAsync(string patientId)
    {
        var entities = await _database.EncounterMappingRepository.FindAsync(m => m.PatientId == patientId);
        foreach (var entity in entities)
        {
            _database.EncounterMappingRepository.Remove(entity);
        }
        await _database.SaveChangesAsync();
    }

    public async Task DeleteByEncounterIdAndFacilityIdAsync(string encounterId, string facilityId)
    {
        await _dbContext.EncounterLocations
            .Where(el => el.EncounterMapping.EncounterId == encounterId && el.EncounterMapping.FacilityId == facilityId)
            .ExecuteDeleteAsync();
        await _dbContext.EncounterMappings
            .Where(em => em.EncounterId == encounterId && em.FacilityId == facilityId)
            .ExecuteDeleteAsync();

        await _dbContext.SaveChangesAsync();
    }

    private static EncounterMappingModel ProjectToModel(EncounterMapping entity)
    {
        return new EncounterMappingModel
        {
            EncounterMappingId = entity.EncounterMappingId,
            FacilityId = entity.FacilityId,
            PatientId = entity.PatientId,
            EncounterId = entity.EncounterId,
            MappedToOrg = entity.MappedToOrg,
            CreateDate = entity.CreateDate,
            ModifiedDate = entity.ModifiedDate,
            EncounterLocations = entity.EncounterLocations?.Select(l => new EncounterLocationModel
            {
                EncounterLocationId = l.EncounterLocationId,
                EncounterMappingId = l.EncounterMappingId,
                OrganizationLocationMappingId = l.OrganizationLocationMappingId,
                CreateDate = l.CreateDate,
                ModifiedDate = l.ModifiedDate
            }).ToList() ?? new List<EncounterLocationModel>()
        };
    }
}
