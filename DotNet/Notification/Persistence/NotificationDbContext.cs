using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Notification.Persistence;

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

    public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
    {
        public NotificationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
            optionsBuilder.UseSqlServer();

            return new NotificationDbContext(optionsBuilder.Options);
        }
    }
}