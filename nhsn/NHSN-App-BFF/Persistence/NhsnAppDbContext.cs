using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence;

public class NhsnAppDbContext : DbContext
{
    public NhsnAppDbContext(DbContextOptions<NhsnAppDbContext> options) : base(options)
    {
    }

    public DbSet<NhsnUser> Users { get; set; } = null!;
    public DbSet<NhsnFacility> Facilities { get; set; } = null!;
    public DbSet<OnboardingDraft> OnboardingDrafts { get; set; } = null!;
    public DbSet<Acknowledgement> Acknowledgements { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NhsnAppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public class Factory : IDesignTimeDbContextFactory<NhsnAppDbContext>
    {
        public NhsnAppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<NhsnAppDbContext>();
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NHSN-App-BFF;Trusted_Connection=True;TrustServerCertificate=True;");
            return new NhsnAppDbContext(optionsBuilder.Options);
        }
    }
}