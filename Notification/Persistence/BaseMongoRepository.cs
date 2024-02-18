using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Domain;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;
using static LantanaGroup.Link.Notification.Settings.NotificationConstants;

namespace LantanaGroup.Link.Notification.Persistence
{
    public class BaseMongoRepository<T> : IBaseRepository<T> where T : BaseEntity
    {
        private readonly ILogger<BaseMongoRepository<T>> _logger;
        protected readonly IMongoCollection<T> _collection;
        protected readonly IMongoDatabase _database;
        protected readonly MongoClient _client;

        public BaseMongoRepository(IOptions<MongoConnection> mongoSettings, ILogger<BaseMongoRepository<T>> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //var settings = new MongoClientSettings()
            //{
            //    Scheme = ConnectionStringScheme.MongoDBPlusSrv,                
            //    Server = new MongoServerAddress(mongoSettings.Value.ConnectionString, 27017),
            //    ConnectTimeout = new TimeSpan(0, 0, 60),
            //    UseTls = true,
            //    ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber())
            //};

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

        protected void BuildIndices(string indexField)
        {
            //if you try to create the same index again, the command is ignored - the duplicated index creation is not possible
            var index = new BsonDocument { { indexField, 1 } };
            var indexModel = new CreateIndexModel<T>(index);
            _collection.Indexes.CreateOne(indexModel);
        }

        public virtual bool Add(T entity)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Add");

            _collection.InsertOne(entity);
            return true;
        }

        public virtual async Task<bool> AddAsync(T entity)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Add Async");

            await _collection.InsertOneAsync(entity);
            return true;
        }

        public bool Exists(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Exists");

            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            T entity = _collection.Find(filter).FirstOrDefault();
            return entity != null;
        }

        public async Task<bool> ExistsAsync(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Exists Async");
           
            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            T entity = await _collection.Find(filter).FirstOrDefaultAsync();
            return entity != null;
        }

        public T Get(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Get");
          
            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            T entity = _collection.Find(filter).FirstOrDefault();
            return entity;
        }

        public IEnumerable<T> GetAll()
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Get All");

            var set = _collection.Find(_ => true);
            return set.ToList();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Get All Async");

            var set = await _collection.FindAsync(_ => true);
            return set.ToList();
        }

        public async Task<T> GetAsync(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Base Repository - Get Async");

            var filter = Builders<T>.Filter.Eq(x => x.Id, id);
            T entity = await _collection.Find(filter).FirstOrDefaultAsync();
            return entity;
        }

        public async Task<bool> HealthCheck()
        {
            try
            {
                await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(NotificationLoggingIds.HealthCheck, "Notification Service - Database Health Check"), ex, "Health check failed for database connection.");
                return false;
            }

            return true;
        }

    }
}
