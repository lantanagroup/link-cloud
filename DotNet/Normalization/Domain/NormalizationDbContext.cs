using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

public partial class NormalizationDbContext : DbContext
{
    public NormalizationDbContext(DbContextOptions<NormalizationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<RetryEntity> EventRetries { get; set; }
    public virtual DbSet<Operation> Operations { get; set; }
    public virtual DbSet<OperationResourceType> OperationResourceTypes { get; set; }
    public virtual DbSet<ResourceType> ResourceTypes { get; set; }
    public virtual DbSet<OperationSequence> OperationSequences { get; set; }
    public virtual DbSet<VendorOperationPreset> VendorOperationPresets { get; set; }
    public virtual DbSet<VendorPresetOperationResourceType> VendorPresetOperationResourceTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //Retry Repository
        modelBuilder.Entity<RetryEntity>()
            .Property(x => x.Headers)
            .HasConversion(
                           v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                                          v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions())
                                                 );

        modelBuilder.Entity<Operation>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<OperationResourceType>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.Operation).WithMany(p => p.OperationResourceTypes).HasConstraintName("FK_OperationResourceTypes_Operation");

            entity.HasOne(d => d.ResourceType).WithMany(p => p.OperationResourceTypes).HasConstraintName("FK_OperationResourceTypes_ResourceType");
        });

        modelBuilder.Entity<OperationSequence>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.OperationResourceType).WithMany(p => p.OperationSequences).HasConstraintName("FK_OperationSequence_OperationResourceTypes");
        });

        modelBuilder.Entity<ResourceType>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
        });

        modelBuilder.Entity<VendorOperationPreset>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<VendorPresetOperationResourceType>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.OperationResourceType).WithMany(p => p.VendorPresetOperationResourceTypes).HasConstraintName("FK_VendorPresetOperationResourceTypes_OperationResourceTypes");

            entity.HasOne(d => d.VendorOperationPreset).WithMany(p => p.VendorPresetOperationResourceTypes).HasConstraintName("FK_VendorPresetOperationResourceTypes_VendorOperationPreset");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    public class NormalizationDbContextFactory : IDesignTimeDbContextFactory<NormalizationDbContext>
    {
        public NormalizationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<NormalizationDbContext>();
            optionsBuilder.UseSqlServer();

            return new NormalizationDbContext(optionsBuilder.Options);
        }
    }
}