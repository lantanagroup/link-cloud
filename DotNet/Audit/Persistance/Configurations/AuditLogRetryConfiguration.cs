using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace LantanaGroup.Link.Audit.Persistance.Configurations
{
    public class AuditLogRetryConfiguration : IEntityTypeConfiguration<RetryEntity>
    {
        public void Configure(EntityTypeBuilder<RetryEntity> builder)
        {
            ConfigureAuditLogRetryTable(builder);;
        }

        private void ConfigureAuditLogRetryTable(EntityTypeBuilder<RetryEntity> builder)
        {        
            //table name pulled from RetryEntity table attribute

            builder.HasKey(x => x.Id).IsClustered(false);

            builder.Property(x => x.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions())
        );
        }
    }
}
