﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.Text.Json;
using LantanaGroup.Link.Normalization.Domain.JsonObjects;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Entities;

public partial class NormalizationDbContext : DbContext
{
    //public NormalizationDbContext()
    //{

    //}

    public NormalizationDbContext(DbContextOptions<NormalizationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<NormalizationConfig> NormalizationConfigs { get; set; }
    public virtual DbSet<RetryEntity> RetryEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NormalizationConfig>(entity =>
        {
            entity.Property(e => e.CreateDate).HasDefaultValueSql("(getutcdate())");
        });

        OnModelCreatingPartial(modelBuilder);

        modelBuilder.Entity<NormalizationConfig>()
            .Property(b => b.OperationSequence)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, INormalizationOperation>>(v, new JsonSerializerOptions())
            );

        //Retry Repository
        modelBuilder.Entity<RetryEntity>()
            .Property(x => x.Headers)
            .HasConversion(
                           v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                                          v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions())
                                                 );
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}