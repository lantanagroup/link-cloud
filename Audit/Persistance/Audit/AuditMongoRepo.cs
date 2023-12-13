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
        private readonly IMongoCollection<AuditEntity> _collection;
        private readonly IMongoDatabase _database;
        private readonly MongoClient _client;
        private readonly ILogger<AuditMongoRepo> _logger;
        private readonly List<string> _sortableColumns = new() { "EventDate" }; //TODO: Make this configurable

        public AuditMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<AuditMongoRepo> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _client = new MongoClient(mongoSettings.Value.ConnectionString);
            _database = _client.GetDatabase(mongoSettings.Value.DatabaseName);
            _collection = _database.GetCollection<AuditEntity>(GetCollectionName(typeof(AuditEntity)));

            //build out indexes
            foreach(var col in _sortableColumns)
            {
                BuildIndices(col);
            }

            //create text index
            var indexDefinition = Builders<AuditEntity>.IndexKeys.Text(f => f.Notes);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<AuditEntity>(indexDefinition));

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
            var indexModel = new CreateIndexModel<AuditEntity>(index);
            _collection.Indexes.CreateOneAsync(indexModel);
        }      
        
        public bool Add(AuditEntity entity)
        {
            _collection.InsertOne(entity);
            return true;
        }

        public async Task<bool> AddAsync(AuditEntity entity)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Repository - Insert One Async");
            await _collection.InsertOneAsync(entity);
            return true;
        }

        public (IEnumerable<AuditEntity>, PaginationMetadata) Find(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber)
        {
            try 
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<AuditEntity> filterBuilder = Builders<AuditEntity>.Filter;
                FilterDefinition<AuditEntity> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<AuditEntity>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<AuditEntity>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<AuditEntity>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterCorrelationBy))
                {
                    var correlationFilter = Builders<AuditEntity>.Filter.Eq(x => x.CorrelationId, filterCorrelationBy);
                    filter &= correlationFilter;
                }

                if (!string.IsNullOrEmpty(filterActionBy))
                {
                    var actionFilter = Builders<AuditEntity>.Filter.Eq(x => x.Action, filterActionBy);
                    filter &= actionFilter;
                }

                if (!string.IsNullOrEmpty(filterUserBy))
                {
                    var userIdFilter = Builders<AuditEntity>.Filter.Eq(x => x.UserId, filterUserBy);
                    var userNameFilter = Builders<AuditEntity>.Filter.Eq(x => x.User, filterUserBy);
                    filter &= userIdFilter | userNameFilter;
                }                

                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<AuditEntity> sortBuilder = Builders<AuditEntity>.Sort;
                List<SortDefinition<AuditEntity>> sortDefinitions = new List<SortDefinition<AuditEntity>>();

                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }
 
                //combine sort definitions
                SortDefinition<AuditEntity> sortDef = sortBuilder.Combine(sortDefinitions);

                #endregion

                //get total count
                long count = 0;
                using (ServiceActivitySource.Instance.StartActivity("Get total count"))
                {
                    count = _collection.CountDocuments(filter);
                }
                PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

                //filter collection            
                List<AuditEntity> auditEvents = new List<AuditEntity>();
                using (ServiceActivitySource.Instance.StartActivity("Get filtered list"))
                {
                    auditEvents = _collection.Find(filter).Sort(sortDef).Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToList();
                }

                return (auditEvents, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(new EventId(AuditLoggingIds.ListItems, "Audit Service - List events"), ex, "Failed to execute find audit events.");
                var repoEx = new ApplicationException("Failed to execute the request to find audit events.", ex);
                //repoEx.Data.Add("searchText", searchText);
                repoEx.Data.Add("filterFacilityBy", filterActionBy);
                repoEx.Data.Add("filterCorrelationBy", filterCorrelationBy);
                repoEx.Data.Add("filterServiceBy", filterServiceBy);
                repoEx.Data.Add("filterActionBy", filterActionBy);
                repoEx.Data.Add("filterUserBy", filterUserBy);
                repoEx.Data.Add("sortBy", sortBy);
                repoEx.Data.Add("pageSize", pageSize);
                repoEx.Data.Add("pageNumber", pageNumber);
                throw repoEx;
            }
            
        }

        public async Task<(IEnumerable<AuditEntity>, PaginationMetadata)> FindAsync(string? searchText, string? filterFacilityBy, string? filterCorrelationBy, string? filterServiceBy, string? filterActionBy, string? filterUserBy, string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Repository - Find Async");

            try 
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<AuditEntity> filterBuilder = Builders<AuditEntity>.Filter;
                FilterDefinition<AuditEntity> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<AuditEntity>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }
    
                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<AuditEntity>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterCorrelationBy))
                {
                    var correlationFilter = Builders<AuditEntity>.Filter.Eq(x => x.CorrelationId, filterCorrelationBy);
                    filter &= correlationFilter;
                }

                if (!string.IsNullOrEmpty(filterServiceBy))
                {
                    var serviceFilter = Builders<AuditEntity>.Filter.Eq(x => x.ServiceName, filterServiceBy);
                    filter &= serviceFilter;
                }

                if (!string.IsNullOrEmpty(filterActionBy))
                {
                    var actionFilter = Builders<AuditEntity>.Filter.Eq(x => x.Action, filterActionBy);
                    filter &= actionFilter;
                }

                if (!string.IsNullOrEmpty(filterUserBy))
                {
                    var userIdFilter = Builders<AuditEntity>.Filter.Eq(x => x.UserId, filterUserBy);
                    var userNameFilter = Builders<AuditEntity>.Filter.Eq(x => x.User, filterUserBy);
                    filter &= userIdFilter | userNameFilter;
                }
                              

                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<AuditEntity> sortBuilder = Builders<AuditEntity>.Sort;
                List<SortDefinition<AuditEntity>> sortDefinitions = new List<SortDefinition<AuditEntity>>();

                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }

                //combine sort definitions
                SortDefinition<AuditEntity> sortDef = sortBuilder.Combine(sortDefinitions);

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
                List<AuditEntity> auditEvents = new List<AuditEntity>();
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
                _logger.LogDebug(new EventId(AuditLoggingIds.ListItems, "Audit Service - List events"), ex, "Failed to execute find audit events asynchronously.");
                var repoEx = new Exception("Failed to execute the request to find audit events.", ex);
                //repoEx.Data.Add("searchText", searchText);
                repoEx.Data.Add("filterFacilityBy", filterFacilityBy);
                repoEx.Data.Add("filterCorrelationBy", filterCorrelationBy);
                repoEx.Data.Add("filterServiceBy", filterServiceBy);
                repoEx.Data.Add("filterActionBy", filterActionBy);
                repoEx.Data.Add("filterUserBy", filterUserBy);
                repoEx.Data.Add("sortBy", sortBy);
                repoEx.Data.Add("pageSize", pageSize);
                repoEx.Data.Add("pageNumber", pageNumber);
                throw repoEx;
            }

                   

        }

        public AuditEntity Get(string id)
        {    
            var filter = Builders<AuditEntity>.Filter.Eq(x => x.Id, id);
            AuditEntity auditEvent = _collection.Find(filter).FirstOrDefault();
            return auditEvent;                            
        }

        public IEnumerable<AuditEntity> GetAll()
        {
            var set = _collection.Find(_ => true);
            return set.ToList();           
        }

        public async Task<IEnumerable<AuditEntity>> GetAllAsync()
        {
            var set = await _collection.FindAsync(_ => true);
            return set.ToList();            
        }

        public async Task<AuditEntity> GetAsync(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Audit Repository - Get By Id Async");
            var filter = Builders<AuditEntity>.Filter.Eq(x => x.Id, id);
            AuditEntity auditEvent = await _collection.Find(filter).FirstOrDefaultAsync();
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
    }
}
