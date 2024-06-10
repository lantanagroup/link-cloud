using LantanaGroup.Link.Submission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.Bson;

namespace LantanaGroup.Link.Submission.Persistence.Configurations
{
    public class TenantSubmissionConfiguration : IEntityTypeConfiguration<TenantSubmissionConfigEntity>
    {
        public void Configure(EntityTypeBuilder<TenantSubmissionConfigEntity> builder)
        {
            ConfigureSubmissionTable(builder);
        }

        private void ConfigureSubmissionTable(EntityTypeBuilder<TenantSubmissionConfigEntity> builder)
        {
            builder.ToTable("TenantSubmissionConfigs");

            builder.HasKey(b => b.Id).IsClustered(false);
            builder.Property(b => b.Id)
            .ValueGeneratedNever()
            .HasConversion(
                id => id.Value,
                value => new TenantSubmissionConfigEntityId(value));


            builder.Property(b => b.FacilityId);

            builder.Property(b => b.ReportType);

            builder.OwnsMany(b => b.Methods, navBuilder =>
            {
                navBuilder.ToJson();
            });

            builder.Property(b => b.CreateDate);

            builder.Property(b => b.ModifyDate);
        }
    }
}
