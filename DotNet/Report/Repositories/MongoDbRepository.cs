using LantanaGroup.Link.Report.Attributes;
using LantanaGroup.Link.Report.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Reflection;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using MongoDB.Bson;
using static LantanaGroup.Link.Report.Settings.ReportConstants;

namespace LantanaGroup.Link.Report.Repositories
{
    public partial class MongoDbRepository<T> where T : ReportEntity
    {
        private readonly ILogger<MongoDbRepository<T>> _logger;
        protected readonly IMongoCollection<T> _collection;
        protected readonly IMongoDatabase _database;
        protected readonly MongoClient _client;

        public MongoDbRepository(IOptions<MongoConnection> mongoSettings, ILogger<MongoDbRepository<T>> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _client = new MongoClient(
                mongoSettings.Value.ConnectionString);

            _database = _client.GetDatabase(
                mongoSettings.Value.DatabaseName);

            _collection = _database.GetCollection<T>(GetCollectionName());

        }

        protected string GetCollectionName()
        {
            return typeof(T).GetTypeInfo().GetCustomAttribute<BsonCollectionAttribute>()?.Name;
        }

        public bool Add(T entity)
        {
            _collection.InsertOne(entity);
            return true;
        }

        public async Task<bool> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                entity.Id = Guid.NewGuid().ToString();
            }

            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return true;
        }

        public void Delete(string id)
        {
            _collection.DeleteOne(x => x.Id == (string)id);
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            await _collection.DeleteOneAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            return await _collection.FindAsync(filter);
        }

        public T Get(string id)
        {
            return _collection.Find(x => x.Id == id).FirstOrDefault();
        }

        public async Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            return (await _collection.FindAsync(x => x.Id == id, cancellationToken: cancellationToken)).FirstOrDefault();
        }

        public T Update(T entity)
        {
            var res = _collection.ReplaceOne(x => x.Id == entity.Id, entity);
            if (res.MatchedCount < 1)
            {
                throw new Exception($"No record found with id {entity.Id}");
            }
            return entity;
        }

        public async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            var res = await _collection.ReplaceOneAsync(x => x.Id == entity.Id, entity, cancellationToken: cancellationToken);
            if (res.MatchedCount < 1)
            {
                throw new Exception($"No record found with id {entity.Id}");
            }
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
                _logger.LogError(new EventId(MeasureReportLoggingIds.HealthCheck, "Measure Report Service - Database Health Check"), ex, "Health check failed for database connection.");
                return false;
            }

            return true;
        }
    }

}