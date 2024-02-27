using LantanaGroup.Link.Notification.Application.Interfaces;
using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Notification.Persistence
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
        {
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
    }
}
