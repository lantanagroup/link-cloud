using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Shared.Application.Models.Configs;


namespace LantanaGroup.Link.Tenant.Services
{
    public interface ITenantConfigurationService
    {
        public Task<List<FacilityConfigModel>> GetAsync();

        public List<FacilityConfigModel> Get();

        public Task<FacilityConfigModel> GetAsyncById(string id);

        public FacilityConfigModel GetById(string id);

        public Task UpdateAsync(string id, FacilityConfigModel FacilityConfigModel);

        public void Update(string id, FacilityConfigModel FacilityConfigModel);

        public Task RemoveAsync(string id);

        public void Remove(string id);

        public Task CreateAsync(FacilityConfigModel FacilityConfigModel);

        public void Create(FacilityConfigModel FacilityConfigModel);

    }

    public class TenantConfigurationService: ITenantConfigurationService
    {

       private readonly IMongoCollection<FacilityConfigModel> _facilityCollection;

       public TenantConfigurationService(IOptions<MongoConnection> mongoConnection, ILogger<TenantConfigurationService> logger)
       {

          var mongoClient = new MongoClient(mongoConnection.Value.ConnectionString);

          var mongoDatabase = mongoClient.GetDatabase(mongoConnection.Value.DatabaseName);

          _facilityCollection = mongoDatabase.GetCollection<FacilityConfigModel>(mongoConnection.Value.CollectionName);

       }


        public async Task<List<FacilityConfigModel>> GetAsync() => await _facilityCollection.Find(_ => true).ToListAsync();

        public async Task CreateAsync(FacilityConfigModel FacilityConfigModel)
        {
            List<FacilityConfigModel> facilities = await _facilityCollection.Find(x => x.FacilityId == FacilityConfigModel.FacilityId).ToListAsync();
            if (facilities.Count > 0)
            {
                throw new ApplicationException($"Facility {FacilityConfigModel.FacilityId} already exists");
            }
            await _facilityCollection.InsertOneAsync(FacilityConfigModel);
        }

        public async Task<FacilityConfigModel> GetAsyncById(string id) => await _facilityCollection.Find(x => x.Id == id).FirstOrDefaultAsync();


        public async Task UpdateAsync(string id, FacilityConfigModel FacilityConfigModel)
        {
            List<FacilityConfigModel> facilities = await _facilityCollection.Find(x => x.FacilityId == FacilityConfigModel.FacilityId && x.Id != FacilityConfigModel.Id).ToListAsync();
            if (facilities.Count > 0)
            {
                throw new ApplicationException($"Facility {FacilityConfigModel.FacilityId} already exists");
            }

            await _facilityCollection.ReplaceOneAsync(x => x.Id == id, FacilityConfigModel);
        }

        public async Task RemoveAsync(string id)
        {
            await _facilityCollection.DeleteOneAsync(x => x.Id == id);
        }

        public List<FacilityConfigModel> Get() => _facilityCollection.Find(_ => true).ToList();

        FacilityConfigModel ITenantConfigurationService.GetById(string id)
        {
            return _facilityCollection.Find(x => x.Id == id).FirstOrDefault();
        }

        void ITenantConfigurationService.Update(string id, FacilityConfigModel FacilityConfigModel)
        {
            // TO-DO validation:  faciltyId is unique and scheduled topic is unique for an facilityId
            _facilityCollection.ReplaceOne(x => x.Id == id, FacilityConfigModel);
        }

        void ITenantConfigurationService.Remove(string id)
        {
            _facilityCollection.DeleteOne(x => x.Id == id);
        }

        void ITenantConfigurationService.Create(FacilityConfigModel FacilityConfigModel)
        {
            // TO-DO validation:  faciltyId is unique and scheduled topic is unique for an facilityId
            _facilityCollection.InsertOne(FacilityConfigModel);
        }
    }
}
