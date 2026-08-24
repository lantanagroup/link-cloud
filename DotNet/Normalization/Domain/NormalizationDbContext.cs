using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
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
    public virtual DbSet<VendorVersionOperationPreset> VendorVersionOperationPresets { get; set; }
    public virtual DbSet<HSLOC> HSLOCS { get; set; }

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
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<VendorVersionOperationPreset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_VendorOperationPreset");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
            entity.HasIndex(e => e.VendorVersionId);

            entity.HasOne(d => d.OperationResourceType).WithMany(p => p.VendorVersionOperationPresets)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VendorOperationPreset_OperationResourceTypes");
        });

        modelBuilder.Entity<HSLOC>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.HasIndex(e => new { e.Version, e.HSLOCCode }).IsUnique();
        });

        // Adds Quartz.NET SqlServer schema to EntityFrameworkCore
        modelBuilder.AddQuartz(builder => builder.UseSqlServer());

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