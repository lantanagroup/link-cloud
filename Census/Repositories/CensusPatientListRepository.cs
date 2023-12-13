using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading;

namespace LantanaGroup.Link.Census.Repositories;

public class CensusPatientListRepository : MongoDbRepository<CensusPatientListEntity>, ICensusPatientListRepository
{
    public CensusPatientListRepository(IOptions<MongoConnection>  mongoSettings) : base(mongoSettings)
    {
    }

    public override CensusPatientListEntity Get(string facilityId)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var facilityCensusStr = (string)facilityId;
        var facilityCensus = _collection.Find(x => x.FacilityId == facilityCensusStr).FirstOrDefault();
        return facilityCensus;
    }

    public override async Task<CensusPatientListEntity> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var facilityCensusStr = (string)facilityId;
        var facilityCensus = await (await _collection.FindAsync(x => x.FacilityId == facilityCensusStr)).FirstOrDefaultAsync();
        return facilityCensus;
    }

    public async Task<List<CensusPatientListEntity>> GetActivePatientsForFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var filter = Builders<CensusPatientListEntity>.Filter.And(
            Builders<CensusPatientListEntity>.Filter.Eq(x => x.FacilityId, facilityId),
            Builders<CensusPatientListEntity>.Filter.Where(x => !x.IsDischarged)
            );
        var activePatients = await (await _collection.FindAsync(filter)).ToListAsync();
        return activePatients;
    }

    public async Task<List<CensusPatientListEntity>> GetAllPatientsForFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var filter = Builders<CensusPatientListEntity>.Filter.Eq(x => x.FacilityId, facilityId);
        var allPatients = await(await _collection.FindAsync(filter)).ToListAsync();
        return allPatients;
    }

    public override void Delete(string id)
    {
        throw new NotImplementedException("Deletes not allowed for this collection.");
    }

    public override Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Deletes not allowed for this collection.");
    }

    public override CensusPatientListEntity Update(CensusPatientListEntity entity)
    {
        var filter = Builders<CensusPatientListEntity>.Filter.And(
                Builders<CensusPatientListEntity>.Filter.Eq(x => x.PatientId, entity.PatientId),
                Builders<CensusPatientListEntity>.Filter.Eq(x => x.FacilityId, entity.FacilityId)
            );
        var existingEntity = _collection.Find<CensusPatientListEntity>(filter).FirstOrDefault();

        entity.UpdatedDate = DateTime.UtcNow;

        if (existingEntity != null)
        {
            entity.Id = existingEntity.Id;
            entity.CreatedDate = existingEntity.CreatedDate;
            _collection.ReplaceOne(filter, entity);
        }
        else
        {
            entity.CreatedDate = DateTime.UtcNow;
            entity.UpdatedDate = DateTime.UtcNow;
            Add(entity);
        }
        return entity;
    }

    public override async Task<CensusPatientListEntity> UpdateAsync(CensusPatientListEntity entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CensusPatientListEntity>.Filter.And(
                Builders<CensusPatientListEntity>.Filter.Eq(x => x.PatientId, entity.PatientId),
                Builders<CensusPatientListEntity>.Filter.Eq(x => x.FacilityId, entity.FacilityId)
            );
        var existingEntity = await (await _collection.FindAsync<CensusPatientListEntity>(filter, cancellationToken: cancellationToken)).FirstOrDefaultAsync();

        entity.UpdatedDate = DateTime.UtcNow;

        if (existingEntity != null)
        {
            entity.Id = existingEntity.Id;
            entity.CreatedDate = existingEntity.CreatedDate;
            await _collection.ReplaceOneAsync(filter, entity);
        }
        else
        {
            //entity.Id = Guid.NewGuid().ToString();
            entity.CreatedDate = DateTime.UtcNow;
            entity.UpdatedDate = DateTime.UtcNow;
            await AddAsync(entity);
        }
        return entity;
    }

    private bool CheckIfNullOrString(object value, string varName)
    {
        if (value == null) throw new ArgumentNullException(varName);
        if (value.GetType() != typeof(string)) throw new ArgumentException($"{varName} is required to be a string!");
        return true;
    }
}
