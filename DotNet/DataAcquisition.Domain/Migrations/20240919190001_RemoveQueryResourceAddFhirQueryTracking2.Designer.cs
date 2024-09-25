﻿// <auto-generated />
using System;
using LantanaGroup.Link.DataAcquisition.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20240919190001_RemoveQueryResourceAddFhirQueryTracking2")]
    partial class RemoveQueryResourceAddFhirQueryTracking2
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("LantanaGroup.Link.DataAcquisition.Domain.Entities.FhirListConfiguration", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Authentication")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreateDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("EHRPatientLists")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FacilityId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FhirBaseServerUrl")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("ModifyDate")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("fhirListConfiguration");
                });

            modelBuilder.Entity("LantanaGroup.Link.DataAcquisition.Domain.Entities.FhirQuery", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreateDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("FacilityId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("ModifyDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("PatientId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RequestBody")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ResourceType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SearchParams")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("fhirQuery");
                });

            modelBuilder.Entity("LantanaGroup.Link.DataAcquisition.Domain.Entities.FhirQueryConfiguration", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Authentication")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreateDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("FacilityId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FhirServerBaseUrl")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("ModifyDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("QueryPlanIds")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("fhirQueryConfiguration");
                });

            modelBuilder.Entity("LantanaGroup.Link.DataAcquisition.Domain.Entities.QueryPlan", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreateDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("EHRDescription")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FacilityId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("InitialQueries")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LookBack")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("ModifyDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("PlanName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SupplementalQueries")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Type")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("queryPlan");
                });

            modelBuilder.Entity("LantanaGroup.Link.DataAcquisition.Domain.Entities.ReferenceResources", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreateDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("FacilityId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("ModifyDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("ReferenceResource")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ResourceId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ResourceType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("referenceResources");
                });

            modelBuilder.Entity("LantanaGroup.Link.Shared.Application.Models.RetryEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreateDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("FacilityId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Headers")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("RetryCount")
                        .HasColumnType("int");

                    b.Property<DateTime>("ScheduledTrigger")
                        .HasColumnType("datetime2");

                    b.Property<string>("ServiceName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Topic")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("EventRetries");
                });
#pragma warning restore 612, 618
        }
    }
}
