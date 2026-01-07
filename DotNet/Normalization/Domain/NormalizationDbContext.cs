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

    public virtual DbSet<Operation> Operations { get; set; }
    public virtual DbSet<OperationResourceType> OperationResourceTypes { get; set; }
    public virtual DbSet<ResourceType> ResourceTypes { get; set; }
    public virtual DbSet<OperationSequence> OperationSequences { get; set; }
    public virtual DbSet<Vendor> Vendors { get; set; }
    public virtual DbSet<VendorVersion> VendorVersions { get; set; }
    public virtual DbSet<VendorVersionOperationPreset> VendorVersionOperationPresets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

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

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
        });

        modelBuilder.Entity<VendorVersion>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.Vendor).WithMany(p => p.VendorVersions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VendorVersion_Vendor");
        });

        modelBuilder.Entity<VendorVersionOperationPreset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_VendorOperationPreset");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.OperationResourceType).WithMany(p => p.VendorVersionOperationPresets)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VendorOperationPreset_OperationResourceTypes");

            entity.HasOne(d => d.VendorVersion).WithMany(p => p.VendorVersionOperationPresets)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VendorOperationPreset_VendorVersion");
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