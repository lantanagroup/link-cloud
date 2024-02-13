using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Application.Models;
using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;
using static LantanaGroup.Link.Audit.Settings.AuditConstants;

namespace LantanaGroup.Link.Audit.Persistance
{
    public class AuditMongoRepo : IAuditRepository
    {
        private readonly IMongoCollection<AuditLog> _collection;
        private readonly IMongoDatabase _database;
        private readonly MongoClient _client;
        private readonly ILogger<AuditMongoRepo> _logger;
        private readonly List<string> _sortableColumns = new() { "EventDate" }; //TODO: Make this configurable

        public AuditMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<AuditMongoRepo> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _client = new MongoClient(mongoSettings.Value.ConnectionString);
            _database = _client.GetDatabase(mongoSettings.Value.DatabaseName);
            _collection = _database.GetCollection<AuditLog>(GetCollectionName(typeof(AuditLog)));

            //build out indexes
            foreach(var col in _sortableColumns)
            {
                BuildIndices(col);
            }

            //create text index
            var indexDefinition = Builders<AuditLog>.IndexKeys.Text(f => f.Notes);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<AuditLog>(indexDefinition));

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

        private void BuildIndices(string indexField)
        {
            //if you try to create the same index again, the command is ignored - the duplicated index creation is not possible
            var index = new BsonDocument { { indexField, 1 } };
            var indexModel = new CreateIndexModel<AuditLog>(index);
            _collection.Indexes.CreateOneAsync(indexModel);
        }      
        
        public bool Add(AuditLog entity)
        {
            _collection.InsertOne(entity);
            return true;
        }

        public async Task<bool> AddAsync(AuditLog entity)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Repository - Insert One Async");
            await _collection.InsertOneAsync(entity);
            return true;
        }

        public (IEnumerable<AuditLog>, PaginationMetadata) Find(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber)
        {
            try 
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<AuditLog> filterBuilder = Builders<AuditLog>.Filter;
                FilterDefinition<AuditLog> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<AuditLog>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<AuditLog>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<AuditLog>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterCorrelationBy))
                {
                    var correlationFilter = Builders<AuditLog>.Filter.Eq(x => x.CorrelationId, filterCorrelationBy);
                    filter &= correlationFilter;
                }

                if (!string.IsNullOrEmpty(filterActionBy))
                {
                    var actionFilter = Builders<AuditLog>.Filter.Eq(x => x.Action, filterActionBy);
                    filter &= actionFilter;
                }

                if (!string.IsNullOrEmpty(filterUserBy))
                {
                    var userIdFilter = Builders<AuditLog>.Filter.Eq(x => x.UserId, filterUserBy);
                    var userNameFilter = Builders<AuditLog>.Filter.Eq(x => x.User, filterUserBy);
                    filter &= userIdFilter | userNameFilter;
                }

                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<AuditLog> sortBuilder = Builders<AuditLog>.Sort;
                List<SortDefinition<AuditLog>> sortDefinitions = new List<SortDefinition<AuditLog>>();

                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }

                //combine sort definitions
                SortDefinition<AuditLog> sortDef = sortBuilder.Combine(sortDefinitions);

                #endregion

                //get total count
                long count = 0;
                using (ServiceActivitySource.Instance.StartActivity("Get total count"))
                {
                    count = _collection.CountDocuments(filter);
                }
                PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

                //filter collection            
                List<AuditLog> auditEvents = new List<AuditLog>();
                using (ServiceActivitySource.Instance.StartActivity("Get filtered list"))
                {
                    auditEvents = _collection.Find(filter).Sort(sortDef).Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToList();
                }

                return (auditEvents, metadata);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }
                     
        }

        public async Task<(IEnumerable<AuditLog>, PaginationMetadata)> FindAsync(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Repository - Find Async");
            try
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<AuditLog> filterBuilder = Builders<AuditLog>.Filter;
                FilterDefinition<AuditLog> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<AuditLog>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<AuditLog>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterCorrelationBy))
                {
                    var correlationFilter = Builders<AuditLog>.Filter.Eq(x => x.CorrelationId, filterCorrelationBy);
                    filter &= correlationFilter;
                }

                if (!string.IsNullOrEmpty(filterServiceBy))
                {
                    var serviceFilter = Builders<AuditLog>.Filter.Eq(x => x.ServiceName, filterServiceBy);
                    filter &= serviceFilter;
                }

                if (!string.IsNullOrEmpty(filterActionBy))
                {
                    var actionFilter = Builders<AuditLog>.Filter.Eq(x => x.Action, filterActionBy);
                    filter &= actionFilter;
                }

                if (!string.IsNullOrEmpty(filterUserBy))
                {
                    var userIdFilter = Builders<AuditLog>.Filter.Eq(x => x.UserId, filterUserBy);
                    var userNameFilter = Builders<AuditLog>.Filter.Eq(x => x.User, filterUserBy);
                    filter &= userIdFilter | userNameFilter;
                }


                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<AuditLog> sortBuilder = Builders<AuditLog>.Sort;
                List<SortDefinition<AuditLog>> sortDefinitions = new List<SortDefinition<AuditLog>>();

                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }

                //combine sort definitions
                SortDefinition<AuditLog> sortDef = sortBuilder.Combine(sortDefinitions);

                #endregion

                //get total count
                long count = 0;
                using (ServiceActivitySource.Instance.StartActivity("Get total count"))
                {
                    count = await _collection.CountDocumentsAsync(filter);
                }
                PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

                //filter collection
                //var timer = new Stopwatch();
                //timer.Start();
                List<AuditLog> auditEvents = new List<AuditLog>();
                using (ServiceActivitySource.Instance.StartActivity("Get filtered list"))
                {
                    auditEvents = await _collection.Find(filter).Sort(sortDef).Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToListAsync();
                }

                //timer.Stop();
                //_logger.LogDebug(AuditLoggingIds.ListItems, "Finding audit events finished in {milliseconds} milliseconds", timer.ElapsedMilliseconds);                
                return (auditEvents, metadata);
            }
            catch(Exception ex)
            {
                var currentActivity = Activity.Current;
                currentActivity?.SetStatus(ActivityStatusCode.Error);
                throw;
            }

        }

        public AuditLog Get(AuditId id)
        {    
            var filter = Builders<AuditLog>.Filter.Eq(x => x.Id, id);
            AuditLog auditEvent = _collection.Find(filter).FirstOrDefault();
            return auditEvent;                            
        }

        public IEnumerable<AuditLog> GetAll()
        {
            var set = _collection.Find(_ => true);
            return set.ToList();           
        }

        public async Task<IEnumerable<AuditLog>> GetAllAsync()
        {
            var set = await _collection.FindAsync(_ => true);
            return set.ToList();            
        }

        public async Task<AuditLog> GetAsync(AuditId id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Repository - Get By Id Async");
            var filter = Builders<AuditLog>.Filter.Eq(x => x.Id, id);
            AuditLog auditEvent = await _collection.Find(filter).FirstOrDefaultAsync();
            return auditEvent;            
        }

        public async Task<bool> HealthCheck()
        {
            try 
            {
                await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(AuditLoggingIds.HealthCheck, "Audit Service - Database Health Check"), ex, "Health check failed for database connection.");
                return false;
            }

            return true;
        }

        Task<bool> IAuditRepository.Add(AuditLog entity)
        {
            throw new NotImplementedException();
        }

        Task<AuditLog> IAuditRepository.Get(AuditId id)
        {
            throw new NotImplementedException();
        }

        public Task<(IEnumerable<AuditLog>, PaginationMetadata)> GetByFacility(string facilityId, int pageSize, int pageNumber)
        {
            throw new NotImplementedException();
        }

        public Task<(IEnumerable<AuditLog>, PaginationMetadata)> Search(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber)
        {
            throw new NotImplementedException();
        }
    }
}
