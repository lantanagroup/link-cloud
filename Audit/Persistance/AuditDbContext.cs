using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Audit.Persistance.Configurations;
using Microsoft.EntityFrameworkCore;

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
        }
    }
}
