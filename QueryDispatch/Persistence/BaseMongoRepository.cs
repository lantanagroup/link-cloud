using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static QueryDispatch.Application.Settings.QueryDispatchConstants;

namespace LantanaGroup.Link.QueryDispatch.Persistence
{
    public class BaseMongoRepository<T> : IBaseRepository<T> where T : BaseQueryEntity
    {
        private readonly ILogger<BaseMongoRepository<T>> _logger;
        protected readonly IMongoCollection<T> _collection;
        protected readonly IMongoDatabase _database;
        protected readonly MongoClient _client;

        public BaseMongoRepository(IOptions<MongoConnection> mongoSettings, ILogger<BaseMongoRepository<T>> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = new MongoClient(mongoSettings.Value.ConnectionString);
            _database = _client.GetDatabase(mongoSettings.Value.DatabaseName);
            _collection = _database.GetCollection<T>(GetCollectionName(typeof(T)));
        }

        /// <summary>
        /// Method for getting the collection name from the domain entity custom collection attribute
        /// </summary>
        /// <param name="documentType"></param>
        /// <returns></returns>
        private protected string GetCollectionName(Type documentType)
        {
            return (documentType.GetCustomAttributes(typeof(BsonCollectionAttribute), true).FirstOrDefault() as BsonCollectionAttribute)?.CollectionName;
        }


        public virtual bool Add(T entity)
        {
            try
            {
                _collection.InsertOne(entity);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Add exception: {ex.Message}");
                return false;
            }
        }

        public virtual async Task<bool> AddAsync(T entity)
        {
            try
            {
                await _collection.InsertOneAsync(entity);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"AddAsync exception: {ex.Message}");
                return false;
            }
        }

        public bool Exists(string facilityId)
        {
            try
            {
                var filter = Builders<T>.Filter.Eq(x => x.FacilityId, facilityId);
                T entity = _collection.Find(filter).FirstOrDefault();
                return entity != null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute check if {typeof(T).Name} with an id of {facilityId} exists.", ex);
                throw new ApplicationException($"Failed to execute the request to check if the specified {typeof(T).Name} exists.");
            }
        }

        public async Task<bool> ExistsAsync(string id)
        {
            try
            {
                var filter = Builders<T>.Filter.Eq(x => x.Id, id);
                T entity = await _collection.Find(filter).FirstOrDefaultAsync();
                return entity != null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute check if {typeof(T).Name} with an id of {id} exists asynchronously.", ex);
                throw new ApplicationException($"Failed to execute the request to check if the specified {typeof(T).Name} exists.");
            }
        }

        public T Get(string id)
        {
            try
            {
                var filter = Builders<T>.Filter.Eq(x => x.Id, id);
                T entity = _collection.Find(filter).FirstOrDefault();
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute get {typeof(T).Name} by id {id}.", ex);
                throw new ApplicationException($"Failed to execute the request to get the specified {typeof(T).Name}.");
            }
        }

        public IEnumerable<T> GetAll()
        {
            try
            {
                var set = _collection.Find(_ => true);
                return set.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute get all {typeof(T).Name}s.", ex);
                throw new ApplicationException($"Failed to execute the request to get all {typeof(T).Name}s.");
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                var set = await _collection.FindAsync(_ => true);
                return set.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute get all {typeof(T).Name}s asynchronously.", ex);
                throw new ApplicationException($"Failed to execute the request to get all {typeof(T).Name}s.");
            }
        }

        public async Task<T> GetAsync(string facilityId)
        {
            try
            {
                var filter = Builders<T>.Filter.Eq(x => x.FacilityId, facilityId);
                T entity = await _collection.Find(filter).FirstOrDefaultAsync();
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to execute get {typeof(T).Name} by id {facilityId} asynchronously.", ex);
                throw new ApplicationException($"Failed to execute the request to get the specified {typeof(T).Name}.");
            }
        }

        public async Task<bool> HealthCheck()
        {
            try
            {
                await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(QueryDispatchLoggingIds.HealthCheck, "Query Dispatch Service - Database Health Check"), ex, "Health check failed for database connection.");
                return false;
            }

            return true;
        }
    }
}
