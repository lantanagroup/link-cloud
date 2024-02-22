using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;
using System.Text.Json;

namespace LantanaGroup.Link.Notification.Persistence
{
    public class NotificationDbContext : DbContext
    {
        private readonly ICreateAuditEventCommand _createAuditEventCommand;

        public NotificationDbContext(DbContextOptions<NotificationDbContext> options, ICreateAuditEventCommand createAuditEventCommand) : base(options)
        {
            _createAuditEventCommand = createAuditEventCommand ?? throw new ArgumentNullException(nameof(createAuditEventCommand));
        }

        public DbSet<NotificationEntity> Notifications { get; set; } = null!;
        public DbSet<NotificationConfig> NotificationConfigs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges()
        {
            PreSaveData();
            var messages = GetAuditMessages();
            foreach(var message in messages)
            {
                _ = Task.Run(() => _createAuditEventCommand.Execute(message.FacilityId, message));
            }          
            return base.SaveChanges();
        }

        public Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {           
            var sortKey = sortBy switch
            {
                "FacilityId" => "FacilityId",
                "NotificationType" => "NotificationType",
                "CreatedOn" => "CreatedOn",
                "SentOn" => "SentOn",
                _ => "CreatedOn"
            };

            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;           
        }

        /// <summary>
        /// Handles any final entity changes before save is executed.
        /// Ensures auditing fields are correctly populated regardless of any supplied values.
        /// </summary>
        protected void PreSaveData()
        {
            foreach (var changed in ChangeTracker.Entries())
            {

                if (changed.Entity is IBaseEntity entity)
                {
                    switch (changed.State)
                    {
                        // Entity modified -- set LastModifiedOn and ensure original CreatedOn is retained
                        case EntityState.Modified:
                            entity.CreatedOn = changed.OriginalValues.GetValue<DateTime>(nameof(entity.CreatedOn));
                            entity.LastModifiedOn = DateTime.UtcNow;
                            break;
                        // Entity created -- set CreatedOn
                        case EntityState.Added:
                            entity.CreatedOn = DateTime.UtcNow;
                            break;
                    }
                }
            }
        }

        protected List<AuditEventMessage> GetAuditMessages()
        {
            List<AuditEventMessage> changes = new List<AuditEventMessage>();

            // only interested in modified entities for change tracking
            EntityState[] states = { EntityState.Modified };


            foreach (var changed in ChangeTracker.Entries())
            {

                if (!states.Contains(changed.State))
                    continue;

                AuditEventType eventType;               
                string entityType = GetEntityTypeName(changed);

                switch (changed.State)
                {
                    case EntityState.Modified:
                        eventType = AuditEventType.Update;                       
                        break;
                    case EntityState.Deleted:
                        eventType = AuditEventType.Delete;                     
                        break;
                    case EntityState.Added:
                    default:
                        eventType = AuditEventType.Create;                       
                        break;
                }


                AuditEventMessage msg = new AuditEventMessage()
                {
                    ServiceName = NotificationConstants.ServiceName,
                    EventDate = DateTime.UtcNow,
                    //UserId
                    //User
                    Action = eventType,
                    Resource = entityType
                };


                // process individual property changes if this is an update
                if (eventType == AuditEventType.Update)
                {
                    msg.PropertyChanges = new List<PropertyChangeModel>();

                    foreach (var prop in changed.Properties)
                    {
                        if (prop.Metadata.Name == "LastModifiedOn")
                            break;

                        var oldVal = JsonSerializer.Serialize(prop.OriginalValue);
                        var newVal = JsonSerializer.Serialize(prop.CurrentValue);
                        if (oldVal == newVal)
                            continue;
                        //_logger.LogInformation($"Changed prop {prop.Metadata.Name} -- {oldVal} -> {newVal}");                        

                        msg.PropertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = prop.Metadata.Name,
                            InitialPropertyValue = oldVal,
                            NewPropertyValue = newVal
                        });
                    }
                }
               
                if (changed.Entity is IBaseEntity entity)
                {
                    //Geneate notes 
                    switch (changed.Metadata.ClrType.Name)
                    {
                        case nameof(NotificationEntity):
                            var notification = (NotificationEntity)entity;                            
                            msg.FacilityId = notification.FacilityId;
                            msg.Notes = $"Notification {notification.Id.Value} was {msg.Action?.ToString().ToLower()}d.";
                            //_ = Task.Run(() => _createAuditEventCommand.Execute(notification.FacilityId, msg));
                            break;
                        case nameof(NotificationConfig):
                            var config = (NotificationConfig)entity;                           
                            msg.FacilityId = config.FacilityId;
                            msg.Notes = $"Notification Configuration {config.Id.Value} {msg.Action?.ToString().ToLower()}d.";
                            //_ = Task.Run(() => _createAuditEventCommand.Execute(config.FacilityId, msg));
                            break;
                    }
                }
                // unhandled type that will not produce an audit message
                else
                {
                    continue;
                }

                // done setting up this message
                changes.Add(msg);
            }

            return changes;
        }

        protected string GetEntityTypeName(EntityEntry entityEntry)
        {
            // regular defined entity types
            if (entityEntry.Entity is IBaseEntity entity)
            {
                switch (entity.GetType().Name)
                {
                    case nameof(NotificationEntity):
                        return "Notification";
                    case nameof(NotificationConfig):
                        return "NotificationConfig";                
                    default:
                        return entity.GetType().Name;
                }
            }           

            return entityEntry.Entity.GetType().Name;
        }
    }
}
