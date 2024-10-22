using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Repository.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LantanaGroup.Link.Tenant.Repository.Context;

public class FacilityDbContext : DbContext
{
    public FacilityDbContext(DbContextOptions<FacilityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new FacilityConfigMap());

        modelBuilder.Entity<FacilityConfigModel>()
           .Property(b => b.Id)
           .HasConversion(
               v => new Guid(v),
               v => v.ToString()
           );

    }

    public DbSet<FacilityConfigModel> Facilities { get; set; }

    public class FacilityDbContextFactory : IDesignTimeDbContextFactory<FacilityDbContext>
    {
        public FacilityDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FacilityDbContext>();
            optionsBuilder.UseSqlServer();

            return new FacilityDbContext(optionsBuilder.Options);
        }
    }
}
