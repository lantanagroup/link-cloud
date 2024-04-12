using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace LantanaGroup.Link.Notification.Persistence.Repositories
{
    public class NotificationConfigurationRepository : INotificationConfigurationRepository
    {
        private readonly ILogger<NotificationConfigurationRepository> _logger;
        private readonly NotificationDbContext _dbContext;
        private readonly IAuditEventFactory _auditEventFactory;
        private readonly ICreateAuditEventCommand _createAuditEventCommand;

        public NotificationConfigurationRepository(ILogger<NotificationConfigurationRepository> logger, NotificationDbContext dbContext, ICreateAuditEventCommand createAuditEventCommand, IAuditEventFactory auditEventFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
            _auditEventFactory = auditEventFactory ?? throw new ArgumentNullException(nameof(auditEventFactory));
        }

        public async Task<bool> AddAsync(NotificationConfig entity, CancellationToken cancellationToken = default)
        {
            await _dbContext.NotificationConfigs.AddAsync(entity, cancellationToken);            
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;            
        }

        public async Task<bool> DeleteAsync(NotificationConfigId id, CancellationToken cancellationToken = default)
        {
            var config = await _dbContext.NotificationConfigs.FindAsync(id, cancellationToken);
            if(config is null)
            {
                return false;
            }

            _dbContext.NotificationConfigs.Remove(config);
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;            
        }

        public async Task<NotificationConfig?> GetAsync(NotificationConfigId id, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var config = noTracking ? 
                await _dbContext.NotificationConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) : 
                await _dbContext.NotificationConfigs.FindAsync(id, cancellationToken);
            return config;
        }

        public async Task<NotificationConfig?> GetFacilityNotificationConfigAsync(string facilityId, bool noTracking = false, CancellationToken cancellationToken = default)
        {
            var config = noTracking ? 
                await _dbContext.NotificationConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken) :
                await _dbContext.NotificationConfigs.FirstOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
            return config;
        }

        public async Task<(IEnumerable<NotificationConfig>, PaginationMetadata)> SearchAsync(string? searchText, string? filterFacilityBy, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken = default)
        {
            IEnumerable<NotificationConfig> configs;
            var query = _dbContext.NotificationConfigs.AsNoTracking().AsQueryable();

            #region Build Query
            if (searchText is not null && searchText.Length > 0)
            {
                query = query.Where(x =>
                    (x.FacilityId != null && x.FacilityId.Contains(searchText)) ||
                    (x.EmailAddresses != null && x.EmailAddresses.Contains(searchText))); //||
                    //(x.EnabledNotifications != null && x.EnabledNotifications.Any(y => y.NotificationType.Contains(searchText))) ||
                    //(x.Channels != null && x.Channels.Any(y => y.Name.Contains(searchText))));
            }

            if (filterFacilityBy is not null && filterFacilityBy.Length > 0)
            {
                query = query.Where(x => x.FacilityId == filterFacilityBy);
            }
            #endregion

            var count = await query.CountAsync(cancellationToken);        
            query = sortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<NotificationConfig>(sortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<NotificationConfig>(sortBy)),
                _ => query.OrderBy(x => x.CreatedOn)
            };
            
            configs = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            PaginationMetadata metadata = new PaginationMetadata(pageSize, pageNumber, count);

            var result = (configs, metadata);

            return result;
        }

        public async Task<bool> UpdateAsync(NotificationConfig entity, CancellationToken cancellationToken = default)
        {
            var originalEntity = await GetAsync(entity.Id, false, cancellationToken);
            if (originalEntity == null)
            {
                return false;
            }

            var message = _auditEventFactory.CreateAuditEvent(null, "TODOUSER", "TODOUSER", AuditEventType.Update, nameof(NotificationConfig), string.Empty);
            message.PropertyChanges = [];

            var serializerOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),                
                WriteIndented = false
            };

            originalEntity.FacilityId = entity.FacilityId;
            originalEntity.EmailAddresses = entity.EmailAddresses;
            originalEntity.EnabledNotifications = entity.EnabledNotifications;

            foreach (var changed in _dbContext.ChangeTracker.Entries())
            {
                if(changed.State == EntityState.Modified)
                {                                 
                    foreach (var prop in changed.Properties)
                    {
                        if (prop.Metadata.Name == "LastModifiedOn")
                            break;

                        var oldVal = JsonSerializer.Serialize(prop.OriginalValue, serializerOptions);
                        var newVal = JsonSerializer.Serialize(prop.CurrentValue, serializerOptions);
                        if (oldVal == newVal)
                            continue;                    
                      
                        message.PropertyChanges.Add(new PropertyChangeModel {
                            PropertyName = prop.Metadata.Name,
                            InitialPropertyValue = oldVal,
                            NewPropertyValue = newVal                                    
                        });
                        
                    }
                }
            }

            if (!originalEntity.Channels.SequenceEqual(entity.Channels))
            {
                //update originalEntity.Channels with entity.Channels
                foreach (var channel in entity.Channels)
                {
                    if (!originalEntity.Channels.Any(x => x.Name == channel.Name))
                    {
                        originalEntity.Channels.Add(channel);
                        message.PropertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = "Channels",
                            InitialPropertyValue = string.Empty,
                            NewPropertyValue = JsonSerializer.Serialize(channel, serializerOptions)
                        });
                    }
                    else if (originalEntity.Channels.Any(x => x.Name == channel.Name))
                    {
                        var originalChannel = originalEntity.Channels.First(x => x.Name == channel.Name);   
                        if(originalChannel.Enabled != channel.Enabled)
                        {
                            message.PropertyChanges.Add(new PropertyChangeModel
                            {
                                PropertyName = "Channels",
                                InitialPropertyValue = JsonSerializer.Serialize(originalChannel, serializerOptions),
                                NewPropertyValue = JsonSerializer.Serialize(channel, serializerOptions)
                            });
                            originalChannel.Enabled = channel.Enabled;
                        }                              
                    }
                    else
                    {
                        message.PropertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = "Channels",
                            InitialPropertyValue = JsonSerializer.Serialize(originalEntity.Channels.First(x => x.Name == channel.Name), serializerOptions),
                            NewPropertyValue = "Deleted"
                        });

                        originalEntity.Channels.Remove(originalEntity.Channels.First(x => x.Name == channel.Name));
                    } 
                    
                    //set the state of the originalEntity to modified
                    _dbContext.Entry(originalEntity).State = EntityState.Modified;
                }                     
            }            
                     
            var result = await _dbContext.SaveChangesAsync(cancellationToken) > 0;

            if (result)
            {          
                message.Notes = $"Notification configuration ({entity.Id.Value}) updated for facility '{entity.FacilityId}'.";
                _ = Task.Run(() => _createAuditEventCommand.Execute(entity.FacilityId, message));                
            }

            return result;
        }

        public async Task<bool> ExistsAsync(NotificationConfigId id, CancellationToken cancellationToken = default)
        {
            var res = await _dbContext.NotificationConfigs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (res != null)
                return true;
            return false;
        }

        /// <summary>
        /// Creates a sort expression for the given sortBy parameter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sortBy"></param>
        /// <returns></returns>
        private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy switch
            {
                "FacilityId" => "FacilityId",
                "CreatedOn" => "CreatedOn",
                "LastModifiedOn" => "LastModifiedOn",
                _ => "CreatedOn"
            };

            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }
    }
}
