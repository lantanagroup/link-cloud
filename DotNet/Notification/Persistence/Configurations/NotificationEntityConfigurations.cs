using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Notification.Persistence.Configurations
{
    public class NotificationEntityConfigurations : IEntityTypeConfiguration<NotificationEntity>
    {
        public void Configure(EntityTypeBuilder<NotificationEntity> builder)
        {
            ConfigureNotificationEntityTable(builder);
        }

        private void ConfigureNotificationEntityTable(EntityTypeBuilder<NotificationEntity> builder)
        {
            builder.ToTable("Notifications");

            //configure strongly typed ID
            builder.HasKey(b => b.Id).IsClustered(false); ;
            builder.Property(b => b.Id)
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => new NotificationId(value));

            builder.HasIndex(b => b.FacilityId);

            builder.Property(b => b.NotificationType)
                .HasMaxLength(250);

            builder.Property(b => b.FacilityId)
                .HasMaxLength(150);            

        }
    }
}
