#nullable disable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Migrations
{
    [DbContext(typeof(NhsnAppDbContext))]
    partial class NhsnAppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.27")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities.NhsnUser", b =>
                {
                    b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
                    b.Property<DateTime>("CreatedOn").HasColumnType("datetime2");
                    b.Property<string>("CreatedBy").HasMaxLength(256).HasColumnType("nvarchar(256)");
                    b.Property<string>("Email").IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
                    b.Property<string>("ExternalUserId").IsRequired().HasMaxLength(128).HasColumnType("nvarchar(128)");
                    b.Property<string>("FacilityId").HasMaxLength(64).HasColumnType("nvarchar(64)");
                    b.Property<string>("GroupsRaw").HasMaxLength(256).HasColumnType("nvarchar(256)");
                    b.Property<bool>("IsAdmin").HasColumnType("bit");
                    b.Property<bool>("IsActive").HasColumnType("bit");
                    b.Property<bool>("IsOnboarded").HasColumnType("bit");
                    b.Property<DateTime?>("LastModifiedOn").HasColumnType("datetime2");
                    b.Property<string>("LastModifiedBy").HasMaxLength(256).HasColumnType("nvarchar(256)");
                    b.Property<string>("Name").IsRequired().HasMaxLength(256).HasColumnType("nvarchar(256)");
                    b.HasKey("Id");
                    b.HasIndex("Email");
                    b.HasIndex("ExternalUserId").IsUnique();
                    b.ToTable("Users");
                });

            modelBuilder.Entity("LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities.NhsnFacility", b =>
                {
                    b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
                    b.Property<string>("FacilityId").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
                    b.Property<bool>("IsOnboarded").HasColumnType("bit");
                    b.HasKey("Id");
                    b.HasIndex("FacilityId").IsUnique();
                    b.ToTable("Facilities");
                });
#pragma warning restore 612, 618
        }
    }
}