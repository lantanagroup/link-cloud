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
    }
}
