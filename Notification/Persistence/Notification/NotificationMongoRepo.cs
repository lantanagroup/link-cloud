using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Infrastructure;
using LantanaGroup.Link.Notification.Infrastructure.SearchHelper;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Diagnostics;

namespace LantanaGroup.Link.Notification.Persistence.Notification
{
    public class NotificationMongoRepo : BaseMongoRepository<NotificationEntity>, INotificationRepository
    {
        private readonly ILogger<NotificationMongoRepo> _logger;
        private readonly List<string> _sortableColumns = new() { "CreatedOn" }; //TODO: Make this configurable 

        public NotificationMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<NotificationMongoRepo> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //build out indexes
            foreach (var col in _sortableColumns)
            {
                BuildIndices(col);
            }

            //create text index
            var indexDefinition = Builders<NotificationEntity>.IndexKeys.Combine(
                Builders<NotificationEntity>.IndexKeys.Text(f => f.Subject),
                Builders<NotificationEntity>.IndexKeys.Text(f => f.Body));           
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<NotificationEntity>(indexDefinition));
            
        }

        public async Task<bool> SetNotificationSentOn(string id)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Notification Repository - Set Sent On Date");
    
            NotificationEntity entity = base.Get(id);
                
            if(entity == null) { throw new Exception($"No notification found with id '{id}'");  }
            entity.SentOn = DateTime.UtcNow;
            await _collection.ReplaceOneAsync(x => x.Id == id, entity);

            return true;
        
        }

        public (IEnumerable<NotificationEntity>, PaginationMetadata) Find(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd, string? sortBy, int pageSize, int pageNumber)
        {
            try
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<NotificationEntity> filterBuilder = Builders<NotificationEntity>.Filter;
                FilterDefinition<NotificationEntity> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<NotificationEntity>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<NotificationEntity>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterNotificationTypeBy))
                {
                    var facilityFilter = Builders<NotificationEntity>.Filter.Eq(x => x.NotificationType, filterNotificationTypeBy);
                    filter &= facilityFilter;
                }

                if (createdOnStart != null)
                {
                    var createdOnStartFilter = Builders<NotificationEntity>.Filter.Gte(x => x.CreatedOn, SearchHelper.StartOfDay(createdOnStart));
                    filter &= createdOnStartFilter;
                }

                if (createdOnEnd != null)
                {
                    var createdOnEndFilter = Builders<NotificationEntity>.Filter.Lte(x => x.CreatedOn, SearchHelper.EndOfDay(createdOnEnd));
                    filter &= createdOnEndFilter;
                }

                if (sentOnStart != null)
                {
                    var sentOnStartFilter = Builders<NotificationEntity>.Filter.Gte(x => x.SentOn, SearchHelper.StartOfDay(sentOnStart));
                    filter &= sentOnStartFilter;
                }

                if (sentOnEnd != null)
                {
                    var sentOnEndFilter = Builders<NotificationEntity>.Filter.Lte(x => x.SentOn, SearchHelper.EndOfDay(sentOnEnd));
                    filter &= sentOnEndFilter;
                }                

                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<NotificationEntity> sortBuilder = Builders<NotificationEntity>.Sort;
                List<SortDefinition<NotificationEntity>> sortDefinitions = new List<SortDefinition<NotificationEntity>>();

                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }

                //combine sort definitions
                SortDefinition<NotificationEntity> sortDef = sortBuilder.Combine(sortDefinitions);

                #endregion

                //get total count
                long count = 0;
                using (ServiceActivitySource.Instance.StartActivity("Get total count"))
                {
                    count = _collection.CountDocuments(filter);
                }
                PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

                //filter collection
                List<NotificationEntity> notifications = new List<NotificationEntity>();
                using (ServiceActivitySource.Instance.StartActivity("Get filtered list"))
                {
                    notifications = _collection.Find(filter).Sort(sortDef).Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToList();
                }

                return (notifications, metadata);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }

        }

        public async Task<(IEnumerable<NotificationEntity>, PaginationMetadata)> FindAsync(string? searchText, string? filterFacilityBy, string? filterNotificationTypeBy, DateTime? createdOnStart, DateTime? createdOnEnd, DateTime? sentOnStart, DateTime? sentOnEnd,  string? sortBy, int pageSize, int pageNumber)
        {
            using Activity? activity = ServiceActivitySource.Instance.StartActivity("Notification Repository - Find Async");

            try
            {
                #region Create Filters
                //create filter bulder and definition
                FilterDefinitionBuilder<NotificationEntity> filterBuilder = Builders<NotificationEntity>.Filter;
                FilterDefinition<NotificationEntity> filter = filterBuilder.Empty;

                if (!string.IsNullOrEmpty(searchText))
                {
                    var searchTextFilter = Builders<NotificationEntity>.Filter.Text(searchText);
                    filter &= searchTextFilter;
                }

                if (!string.IsNullOrEmpty(filterFacilityBy))
                {
                    var facilityFilter = Builders<NotificationEntity>.Filter.Eq(x => x.FacilityId, filterFacilityBy);
                    filter &= facilityFilter;
                }

                if (!string.IsNullOrEmpty(filterNotificationTypeBy))
                {
                    var facilityFilter = Builders<NotificationEntity>.Filter.Eq(x => x.NotificationType, filterNotificationTypeBy);
                    filter &= facilityFilter;
                }

                if (createdOnStart != null)
                {
                    var createdOnStartFilter = Builders<NotificationEntity>.Filter.Gte(x => x.CreatedOn, SearchHelper.StartOfDay(createdOnStart));
                    filter &= createdOnStartFilter;
                }

                if (createdOnEnd != null)
                {
                    var createdOnEndFilter = Builders<NotificationEntity>.Filter.Lte(x => x.CreatedOn, SearchHelper.EndOfDay(createdOnEnd));
                    filter &= createdOnEndFilter;
                }

                if (sentOnStart != null)
                {
                    var sentOnStartFilter = Builders<NotificationEntity>.Filter.Gte(x => x.SentOn, SearchHelper.StartOfDay(sentOnStart));
                    filter &= sentOnStartFilter;
                }

                if (sentOnEnd != null)
                {
                    var sentOnEndFilter = Builders<NotificationEntity>.Filter.Lte(x => x.SentOn, SearchHelper.EndOfDay(sentOnEnd));
                    filter &= sentOnEndFilter;
                }              

                #endregion

                #region Create Sort Definitions
                //TODO: Add sort direction logic
                //create sort builder and definition, if no sort field specified then sort by eventDate descending            
                SortDefinitionBuilder<NotificationEntity> sortBuilder = Builders<NotificationEntity>.Sort;
                List<SortDefinition<NotificationEntity>> sortDefinitions = new List<SortDefinition<NotificationEntity>>();

                if (!string.IsNullOrEmpty(sortBy) && _sortableColumns.Contains(sortBy))
                {
                    //sortBy exists in sortableColumns         
                    sortDefinitions.Add(sortBuilder.Descending(sortBy));
                }

                //combine sort definitions
                SortDefinition<NotificationEntity> sortDef = sortBuilder.Combine(sortDefinitions);

                #endregion

                //get total count
                long count = 0;
                using (ServiceActivitySource.Instance.StartActivity("Get total count"))
                {
                    count = _collection.CountDocuments(filter);
                }
                PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

                //filter collection
                List<NotificationEntity> notifications = new List<NotificationEntity>();
                using (ServiceActivitySource.Instance.StartActivity("Get filtered list"))
                {
                    notifications = _collection.Find(filter).Sort(sortDef).Skip(pageSize * (pageNumber - 1)).Limit(pageSize).ToList();
                }

                return (notifications, metadata);
            }
            catch (Exception ex)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                throw;
            }

        }
    }
}
