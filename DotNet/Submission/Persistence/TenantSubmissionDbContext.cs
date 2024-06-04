using LantanaGroup.Link.Submission.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Submission.Persistence
{
    public class TenantSubmissionDbContext : DbContext
    {
        public TenantSubmissionDbContext(DbContextOptions<TenantSubmissionDbContext> options) : base(options)
        {
        }

        public DbSet<TenantSubmissionConfigEntity> TenantSubmissionConfigEntities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantSubmissionDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
