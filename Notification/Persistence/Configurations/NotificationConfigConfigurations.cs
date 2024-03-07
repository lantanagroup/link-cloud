using LantanaGroup.Link.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Notification.Persistence.Configurations
{
    public class NotificationConfigConfigurations : IEntityTypeConfiguration<NotificationConfig>
    {
        public void Configure(EntityTypeBuilder<NotificationConfig> builder)
        {
            ConfigureNotificationConfigTable(builder);
        }

        private void ConfigureNotificationConfigTable(EntityTypeBuilder<NotificationConfig> builder)
        {
            builder.ToTable("NotificationConfigs");

            //configure strongly typed ID
            builder.HasKey(b => b.Id).IsClustered(false);
            builder.Property(b => b.Id)
                .ValueGeneratedNever()
                .HasConversion(
                    id => id.Value,
                    value => new NotificationConfigId(value));           

            builder.Property(b => b.FacilityId)
                .HasMaxLength(150);

            builder.OwnsMany(b => b.EnabledNotifications, navBuilder =>
            {
                navBuilder.ToJson();
            });

            builder.OwnsMany(b => b.Channels, navBuilder =>
            {
                navBuilder.ToJson();                
            });            

        }
    }
}
