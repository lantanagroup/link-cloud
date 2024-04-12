using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Mapping;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Tenant.Repository.Context
{
    public class FacilityDbContext : DbContext
    {
        public FacilityDbContext(DbContextOptions<FacilityDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new FacilityConfigMap());
        }

        public DbSet<FacilityConfigModel> Facilities { get; set; }
    }
}
