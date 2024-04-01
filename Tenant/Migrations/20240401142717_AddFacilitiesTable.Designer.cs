﻿// <auto-generated />
using System;
using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    [DbContext(typeof(FacilityDbContext))]
    [Migration("20240401142717_AddFacilitiesTable")]
    partial class AddFacilitiesTable
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("LantanaGroup.Link.Tenant.Entities.FacilityConfigModel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedOn")
                        .HasColumnType("datetime2");

                    b.Property<string>("FacilityId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FacilityName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("LastModifiedOn")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("MRPCreatedDate")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("MRPModifyDate")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    SqlServerKeyBuilderExtensions.IsClustered(b.HasKey("Id"), false);

                    b.ToTable("Facilities", (string)null);
                });

            modelBuilder.Entity("LantanaGroup.Link.Tenant.Entities.FacilityConfigModel", b =>
                {
                    b.OwnsMany("LantanaGroup.Link.Tenant.Entities.MonthlyReportingPlanModel", "MonthlyReportingPlans", b1 =>
                        {
                            b1.Property<Guid>("FacilityConfigModelId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<int>("Id")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("int");

                            b1.Property<int?>("ReportMonth")
                                .HasColumnType("int");

                            b1.Property<string>("ReportType")
                                .HasColumnType("nvarchar(max)");

                            b1.Property<int?>("ReportYear")
                                .HasColumnType("int");

                            b1.HasKey("FacilityConfigModelId", "Id");

                            b1.ToTable("Facilities");

                            b1.ToJson("MonthlyReportingPlans");

                            b1.WithOwner()
                                .HasForeignKey("FacilityConfigModelId");
                        });

                    b.OwnsMany("LantanaGroup.Link.Tenant.Entities.ScheduledTaskModel", "ScheduledTasks", b1 =>
                        {
                            b1.Property<Guid>("FacilityConfigModelId")
                                .HasColumnType("uniqueidentifier");

                            b1.Property<int>("Id")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("int");

                            b1.Property<string>("KafkaTopic")
                                .HasColumnType("nvarchar(max)");

                            b1.HasKey("FacilityConfigModelId", "Id");

                            b1.ToTable("Facilities");

                            b1.ToJson("ScheduledTasks");

                            b1.WithOwner()
                                .HasForeignKey("FacilityConfigModelId");

                            b1.OwnsMany("LantanaGroup.Link.Tenant.Entities.ScheduledTaskModel+ReportTypeSchedule", "ReportTypeSchedules", b2 =>
                                {
                                    b2.Property<Guid>("ScheduledTaskModelFacilityConfigModelId")
                                        .HasColumnType("uniqueidentifier");

                                    b2.Property<int>("ScheduledTaskModelId")
                                        .HasColumnType("int");

                                    b2.Property<int>("Id")
                                        .ValueGeneratedOnAdd()
                                        .HasColumnType("int");

                                    b2.Property<string>("ReportType")
                                        .HasColumnType("nvarchar(max)");

                                    b2.Property<string>("ScheduledTriggers")
                                        .HasColumnType("nvarchar(max)");

                                    b2.HasKey("ScheduledTaskModelFacilityConfigModelId", "ScheduledTaskModelId", "Id");

                                    b2.ToTable("Facilities");

                                    b2.ToJson("ReportTypeSchedules");

                                    b2.WithOwner()
                                        .HasForeignKey("ScheduledTaskModelFacilityConfigModelId", "ScheduledTaskModelId");
                                });

                            b1.Navigation("ReportTypeSchedules");
                        });

                    b.Navigation("MonthlyReportingPlans");

                    b.Navigation("ScheduledTasks");
                });
#pragma warning restore 612, 618
        }
    }
}
