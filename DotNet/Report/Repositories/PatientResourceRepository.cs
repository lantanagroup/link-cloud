using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Repositories
{
    public class PatientResourceRepository : MongoDbRepository<PatientResourceModel>
    {
        public PatientResourceRepository(IOptions<MongoConnection> mongoSettings, ILogger<PatientResourceRepository> logger) : base(mongoSettings, logger) 
        { 
        }

        public async Task<PatientResourceModel> GetAsync(string facilityId, string patientId, string resouceId, string resourceType)
        {
            var filter = Builders<PatientResourceModel>.Filter.Eq(x => x.ResourceId, resouceId) 
                & Builders<PatientResourceModel>.Filter.Eq(x => x.ResourceType, resourceType)
                & Builders<PatientResourceModel>.Filter.Eq(x => x.FacilityId, facilityId)
                & Builders<PatientResourceModel>.Filter.Eq(x => x.PatientId, patientId);
            
            var entity = await _collection.Find(filter).FirstOrDefaultAsync();
            
            return entity;
        }
    }
}
