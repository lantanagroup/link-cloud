using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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

        /// <summary>
        /// Creates a sort expression for the given sortBy parameter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sortBy"></param>
        /// <returns></returns>
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
    }
}
