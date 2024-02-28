using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LantanaGroup.Link.Audit.Persistance
{
    public class AuditDbContext : DbContext
    {
        public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
        {          
        }

        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }

        public Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
        {
            var sortKey = sortBy switch
            {
                "FacilityId" => "FacilityId",
                "Action" => "Action",
                "ServiceName" => "ServiceName",
                "Resource" => "Resource",
                "CreatedOn" => "CreatedOn",                
                _ => "CreatedOn"
            };

            var parameter = Expression.Parameter(typeof(T), "p");
            var sortExpression = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(parameter, sortKey), typeof(object)), parameter);

            return sortExpression;
        }

        public override int SaveChanges()
        {
            PreSaveData();            
            return base.SaveChanges();
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
