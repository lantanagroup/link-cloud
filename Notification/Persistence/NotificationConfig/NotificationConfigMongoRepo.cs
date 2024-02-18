using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Persistence
{
    public class NotificationConfigMongoRepo : BaseMongoRepository<NotificationConfig>, INotificationConfigurationRepository
    {
        private readonly ILogger<NotificationConfigMongoRepo> _logger;
        private readonly List<string> _sortableColumns = new() { "CreatedOn", "FacilityId" }; //TODO: Make this configurable       

        public NotificationConfigMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<NotificationConfigMongoRepo> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //build out indexes
            foreach (var col in _sortableColumns)
            {
                BuildIndices(col);
            }

            //create text index
            var indexDefinition = Builders<NotificationConfig>.IndexKeys.Text(f => f.EmailAddresses);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<NotificationConfig>(indexDefinition));
        }

        public async Task<NotificationConfig> GetNotificationConfigByFacilityAsync(string facilityId)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Notification Configuration Repository - Get By Facility Id Async");
         
            var filter = Builders<NotificationConfig>.Filter.Eq(x => x.FacilityId, facilityId);
            NotificationConfig entity = await _collection.Find(filter).FirstOrDefaultAsync();
            return entity;
        }

        public (IEnumerable<NotificationConfig>, PaginationMetadata) Find(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber)
        {
            try
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<NotificationConfig> filterBuilder = Builders<NotificationConfig>.Filter;
                FilterDefinition<NotificationConfig> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<NotificationConfig>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<NotificationConfig>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }                                     

                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<NotificationConfig> sortBuilder = Builders<NotificationConfig>.Sort;
                List<SortDefinition<NotificationConfig>> sortDefinitions = new List<SortDefinition<NotificationConfig>>();

                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }

                //combine sort definitions
                SortDefinition<NotificationConfig> sortDef = sortBuilder.Combine(sortDefinitions);

                #endregion

                //get total count
                long count = 0;
                using (ServiceActivitySource.Instance.StartActivity("Get total count"))
                {
                    count = _collection.CountDocuments(filter);
                }
                PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

                //filter collection
                List<NotificationConfig> configs = new List<NotificationConfig>();
                using (ServiceActivitySource.Instance.StartActivity("Get filtered list"))
                {
                    configs = _collection.Find(filter).Sort(sortDef).Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToList();
                }               

                return (configs, metadata);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;              
            }

        }

        public async Task<(IEnumerable<NotificationConfig>, PaginationMetadata)> FindAsync(string? searchText, string? filterFacilityBy, string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Notification Configuration Repository - Find Async");

            try
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<NotificationConfig> filterBuilder = Builders<NotificationConfig>.Filter;
                FilterDefinition<NotificationConfig> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<NotificationConfig>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<NotificationConfig>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }                               

                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<NotificationConfig> sortBuilder = Builders<NotificationConfig>.Sort;
                List<SortDefinition<NotificationConfig>> sortDefinitions = new List<SortDefinition<NotificationConfig>>();
                
                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }

                //combine sort definitions
                SortDefinition<NotificationConfig> sortDef = sortBuilder.Combine(sortDefinitions);

                #endregion

                //get total count
                long count = 0;
                using (ServiceActivitySource.Instance.StartActivity("Get total count"))
                {
                    count = _collection.CountDocuments(filter);
                }
                PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

                //filter collection
                List<NotificationConfig> configs = new List<NotificationConfig>();
                using (ServiceActivitySource.Instance.StartActivity("Get filtered list"))
                {
                    configs = _collection.Find(filter).Sort(sortDef).Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToList();
                }

                return (configs, metadata);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);                
                throw;
            }

        }

        public bool Update(NotificationConfig config)
        {          
            _collection.ReplaceOne(x => x.Id == config.Id, config);
            return true;
        }

        public async Task<bool> UpdateAsync(NotificationConfig config)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Notification Configuration Repository - Update Async");

            await _collection.ReplaceOneAsync(x => x.Id == config.Id, config);
            return true;            
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Notification Configuration Repository - Delete Async");

            await _collection.DeleteOneAsync(x => x.Id == NotificationConfigId.FromString(id));
            return true;
        }
    }
}
