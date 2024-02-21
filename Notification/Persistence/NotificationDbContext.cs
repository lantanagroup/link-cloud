using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Application.Models;
using LantanaGroup.Link.Notification.Application.Notification.Commands;
using LantanaGroup.Link.Notification.Domain.Entities;
using LantanaGroup.Link.Notification.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
            //_logger.LogInformation($"PreSaveData: {ChangeTracker.Entries().Count()}");

            foreach (var changed in ChangeTracker.Entries())
            {                           

                if (changed.Entity is IBaseEntity entity)
                {
                    AuditEventMessage msg = new AuditEventMessage()
                    {
                        ServiceName = NotificationConstants.ServiceName
                    };

                    switch(changed.State)
                    {
                        // Entity modified -- set LastModifiedOn and ensure original CreatedOn is retained
                        case EntityState.Modified:
                            msg.Action = AuditEventType.Update;
                            entity.CreatedOn = changed.OriginalValues.GetValue<DateTime>(nameof(entity.CreatedOn));
                            entity.LastModifiedOn = DateTime.UtcNow;
                            msg.EventDate = entity.LastModifiedOn;

                            // Collect property changes
                            msg.PropertyChanges = new List<PropertyChangeModel>();
                            foreach (var prop in changed.OriginalValues.Properties)
                            {
                                var original = changed.OriginalValues[prop];
                                var current = changed.CurrentValues[prop];
                                if (!Equals(original, current))
                                {
                                    msg.PropertyChanges.Add(new PropertyChangeModel()
                                    {
                                        PropertyName = prop.Name,
                                        InitialPropertyValue = original?.ToString(),
                                        NewPropertyValue = current?.ToString()
                                    });
                                }
                            }                         
                            break;
                        // Entity created -- set CreatedOn
                        case EntityState.Added:
                            msg.Action = AuditEventType.Create;
                            entity.CreatedOn = DateTime.UtcNow;
                            msg.EventDate = entity.CreatedOn;
                            break;
                        case EntityState.Deleted:
                            msg.Action = AuditEventType.Delete;
                            msg.EventDate = DateTime.UtcNow;
                            // Do nothing
                            break;
                    }

                    //Geneate notes and create audit event
                    switch(changed.Metadata.Name)
                    {
                        case nameof(NotificationEntity):
                            var notification = (NotificationEntity)entity;
                            msg.Resource = nameof(NotificationEntity);
                            msg.Notes = $"Notification {notification.Id.Value} was {msg.Action?.ToString().ToLower()}d.";
                            _ = Task.Run(() => _createAuditEventCommand.Execute(notification.FacilityId, msg));
                            break;
                        case nameof(NotificationConfig):
                            var config = (NotificationConfig)entity;
                            msg.Resource = nameof(NotificationConfig);
                            msg.Notes = $"Notification Configuration {config.Id.Value} {msg.Action?.ToString().ToLower()}d.";
                            _ = Task.Run(() => _createAuditEventCommand.Execute(config.FacilityId, msg));
                            break;
                    }                    
                }                
            }
        }
    }
}
