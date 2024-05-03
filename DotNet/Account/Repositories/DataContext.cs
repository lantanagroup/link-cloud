using Confluent.Kafka;
using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Domain.Interfaces;
using LantanaGroup.Link.Account.Domain.Messages;
using LantanaGroup.Link.Account.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LantanaGroup.Link.Account.Repositories
{
    public class DataContext : DbContext
    {
        private readonly ILogger<DataContext> _logger;
        protected IOptions<KafkaConnection> _kafkaConnection;
        protected IOptions<PostgresConnection> _pgSettings;
        protected string[] BridgeTypes = new string[] { "AccountModelGroupModel", "AccountModelRoleModel", "GroupModelRoleModel" };

        public virtual DbSet<LinkUser> Accounts { get; set; }
        public virtual DbSet<GroupModel> Groups { get; set; }
        public virtual DbSet<LinkRole> Roles { get; set; }

        public DataContext(ILogger<DataContext> logger, IOptions<KafkaConnection> kafkaConnection, IOptions<PostgresConnection> pgSettings, DbContextOptions options) : base(options)
        {
            _logger = logger;
            _kafkaConnection = kafkaConnection;
            _pgSettings = pgSettings;
        }

        public DataContext(ILogger<DataContext> logger, IOptions<KafkaConnection> kafkaConnection, IOptions<PostgresConnection> pgSettings)
        {
            _logger = logger;
            _kafkaConnection = kafkaConnection;
            _pgSettings = pgSettings;

            ChangeTracker.LazyLoadingEnabled = false;
        }        

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_pgSettings.Value.ConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // manually set bridge table names

            modelBuilder.Entity<LinkUser>()
                .HasMany(left => left.Groups)
                .WithMany(right => right.Accounts)
                .UsingEntity(join => join.ToTable("AccountGroups"));

            modelBuilder.Entity<LinkUser>()
                .HasMany(left => left.Roles)
                .WithMany(right => right.Accounts)
                .UsingEntity(join => join.ToTable("AccountRoles"));


            modelBuilder.Entity<GroupModel>()
                .HasMany(left => left.Roles)
                .WithMany(right => right.Groups)
                .UsingEntity(join => join.ToTable("GroupRoles"));


            // Filter query for deleted entities
            modelBuilder.Entity<LinkUser>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<GroupModel>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<LinkRole>().HasQueryFilter(a => !a.IsDeleted);
        }

        public override int SaveChanges()
        {
            PreSaveData();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PreSaveData();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            PreSaveData();
            var messages = GetAuditMessages();
            var res = await base.SaveChangesAsync(cancellationToken);
            _ = ProduceAuditMessages(messages);
            return res;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            PreSaveData();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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

            EntityState[] states = { EntityState.Added, EntityState.Modified, EntityState.Deleted };


            foreach (var changed in ChangeTracker.Entries())
            {

                if (!states.Contains(changed.State))
                    continue;


                AuditEventType eventType;
                string actionLabel;
                string entityType = GetEntityTypeName(changed);

                switch (changed.State)
                {
                    case EntityState.Modified:
                        eventType = AuditEventType.Update;
                        actionLabel = "Updated";
                        break;
                    case EntityState.Deleted:
                        eventType = AuditEventType.Delete;
                        actionLabel = "Deleted";
                        break;
                    case EntityState.Added:
                    default:
                        eventType = AuditEventType.Create;
                        actionLabel = "Created";
                        break;
                }


                AuditEventMessage msg = new AuditEventMessage()
                {
                    ServiceName = AccountConstants.ServiceName,
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
                        var oldVal = JsonSerializer.Serialize(prop.OriginalValue);
                        var newVal = JsonSerializer.Serialize(prop.CurrentValue);
                        if (oldVal == newVal)
                            continue;
                        //_logger.LogInformation($"Changed prop {prop.Metadata.Name} -- {oldVal} -> {newVal}");


                        // soft delete or restore is still an update event so we need to log those differently
                        if (prop.Metadata.Name == "IsDeleted")
                        {
                            if (prop.CurrentValue is bool && prop.OriginalValue is bool)
                            {
                                bool newDeletedVal = (bool)prop.CurrentValue;
                                bool oldDeletedVal = (bool)prop.OriginalValue;

                                // entity is now deleted
                                if (newDeletedVal && !oldDeletedVal)
                                {
                                    msg.Action = AuditEventType.Delete;
                                }
                                // entity is now restored
                                else if (!newDeletedVal && oldDeletedVal)
                                {
                                    msg.Action = AuditEventType.Restore;
                                }
                            }
                        }

                        msg.PropertyChanges.Add(new PropertyChangeModel
                        {
                            PropertyName = prop.Metadata.Name,
                            InitialPropertyValue = oldVal,
                            NewPropertyValue = newVal
                        });
                    }
                }


                // base entity types (accounts/groups/roles)
                if (changed.Entity is IBaseEntity entity)
                {
                    msg.Notes = $"{actionLabel} {entityType} with ID: {entity.Id}";
                }

                // bridge types that EF automatically creates (group and role membership)
                else if (BridgeTypes.Contains(changed.Metadata.Name))
                {

                    string type1, type2, id1, id2;

                    switch (changed.Metadata.Name)
                    {
                        case "AccountModelGroupModel":
                            type1 = "Account";
                            type2 = "Group";
                            id1 = ((Dictionary<string, object>)changed.Entity)["AccountsId"].ToString() ?? "";
                            id2 = ((Dictionary<string, object>)changed.Entity)["GroupsId"].ToString() ?? "";
                            break;
                        case "AccountModelRoleModel":
                            type1 = "Account";
                            type2 = "Role";
                            id1 = ((Dictionary<string, object>)changed.Entity)["AccountsId"].ToString() ?? "";
                            id2 = ((Dictionary<string, object>)changed.Entity)["RolesId"].ToString() ?? "";
                            break;
                        case "GroupModelRoleModel":
                            type1 = "Group";
                            type2 = "Role";
                            id1 = ((Dictionary<string, object>)changed.Entity)["GroupsId"].ToString() ?? "";
                            id2 = ((Dictionary<string, object>)changed.Entity)["RolesId"].ToString() ?? "";
                            break;
                        default:
                            type1 = "Unknown";
                            type2 = "Unknown";
                            id1 = "null";
                            id2 = "null";
                            break;
                    }

                    string action, direction;

                    switch (eventType)
                    {
                        case AuditEventType.Create:
                            action = "Added";
                            direction = "to";
                            break;
                        case AuditEventType.Delete:
                            action = "Removed";
                            direction = "from";
                            break;
                        default:
                            action = eventType.ToString();
                            direction = "";
                            break;
                    }
                    
                    msg.Notes = $"{action} {type1} with ID {id1} {direction} {type2} with ID {id2}";
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
                    case "AccountModel":
                        return "Account";
                    case "GroupModel":
                        return "Group";
                    case "RoleModel":
                        return "Role";
                    default:
                        return entity.GetType().Name;
                }
            }

            // bridge types that EF creates automatically
            if (BridgeTypes.Contains(entityEntry.Metadata.Name))
            {
                switch (entityEntry.Metadata.Name)
                {
                    case "AccountModelGroupModel":
                        return "AccountGroup";
                    case "AccountModelRoleModel":
                        return "AccountRole";
                    case "GroupModelRoleModel":
                        return "GroupRole";
                    default:
                        return entityEntry.Metadata.Name;
                }
            }

            return entityEntry.Entity.GetType().Name;
        }


        protected async Task ProduceAuditMessages(List<AuditEventMessage> messages)
        {

            using (var producer = new ProducerBuilder<string, AuditEventMessage>(_kafkaConnection.Value.CreateProducerConfig()).SetValueSerializer(new JsonWithFhirMessageSerializer<AuditEventMessage>()).Build())
            {
                foreach (var msg in messages)
                {
                    //_logger.LogInformation($"Producing message: {KafkaTopic.AuditableEventOccurred}");

                    var headers = new Headers
                    {
                        { "X-Correlation-Id", Guid.NewGuid().ToByteArray() }
                    };

                    await producer.ProduceAsync(KafkaTopic.AuditableEventOccurred.ToString(), new Message<string, AuditEventMessage>
                    {
                        Value = msg,
                        Headers = headers
                    });
                }
            }

        }

    }
}
