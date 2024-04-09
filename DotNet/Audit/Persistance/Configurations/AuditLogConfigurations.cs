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
            
            builder.HasKey(b => b.Id).IsClustered();

            //configure strongly typed ID
            builder.Property(b => b.AuditId)
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => new AuditId(value));

            builder.HasIndex(b => b.FacilityId);

            builder.HasIndex(b => b.UserId);

            builder.HasIndex(b => b.ServiceName);

            builder.HasIndex(b => b.Action);

            builder.HasIndex(b => b.EventDate);

            builder.HasIndex(b => b.CreatedOn);

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
