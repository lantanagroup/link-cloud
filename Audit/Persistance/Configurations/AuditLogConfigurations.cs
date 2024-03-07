using LantanaGroup.Link.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Audit.Persistance.Configurations
{
    public class AuditLogConfigurations : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            ConfigureAuditLogTable(builder);
        }

        private void ConfigureAuditLogTable(EntityTypeBuilder<AuditLog> builder)
        {           
            builder.ToTable("AuditLogs");

            //configure strongly typed ID
            builder.HasKey(b => b.Id).IsClustered(false);
            builder.Property(b => b.Id)
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => new AuditId(value));

            builder.HasIndex(b => b.FacilityId);

            builder.HasIndex(b => b.UserId);

            builder.Property(b => b.ServiceName)
                .HasMaxLength(100);

            builder.Property(b => b.FacilityId)
                .HasMaxLength(150);

            builder.Property(b => b.Action)
                .HasMaxLength(50);

            //map property changes to JSON column
            builder.OwnsMany(b => b.PropertyChanges, navBuilder =>
            {
                navBuilder.ToJson();
            });

        }
    }
}
