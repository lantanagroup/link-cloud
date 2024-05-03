using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Account.Persistence.Configurations
{
    public class LinkRoleConfiguration : IEntityTypeConfiguration<LinkRole>
    {
        public void Configure(EntityTypeBuilder<LinkRole> builder)
        {
            ConfigureTable(builder);
        }

        private void ConfigureTable(EntityTypeBuilder<LinkRole> builder)
        {
            builder.Property(r => r.Name).HasMaxLength(128);
            builder.Property(r => r.NormalizedName).HasMaxLength(128);

            // Each Role can have many entries in the UserRole join table
            builder.HasMany(e => e.UserRoles)
                .WithOne(e => e.Role)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();

            // Each Role can have many associated RoleClaims
            builder.HasMany(e => e.RoleClaims)
                .WithOne(e => e.Role)
                .HasForeignKey(rc => rc.RoleId)
                .IsRequired();
        }
    }
}
