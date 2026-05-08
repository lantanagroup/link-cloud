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
            builder.ToTable("Roles");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name).HasMaxLength(128);

            // Each Role can have many entries in the UserRole join table
            builder.HasMany(e => e.UserRoles)
                .WithOne(e => e.Role)
                .HasForeignKey(ur => ur.RoleId);

            // Each Role can have many associated RoleClaims
            builder.HasMany(e => e.RoleClaims)
                .WithOne(e => e.Role)
                .HasForeignKey(rc => rc.RoleId);            
        }
    }
}
