using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LantanaGroup.Link.Account.Persistence.Configurations
{
    public class LinkUserConfiguration : IEntityTypeConfiguration<LinkUser>
    {
        public void Configure(EntityTypeBuilder<LinkUser> builder)
        {
            ConfigureTable(builder);
        }

        private void ConfigureTable(EntityTypeBuilder<LinkUser> builder)
        {
            builder.ToTable( name: "Users");

            builder.HasKey(t => t.Id);

            builder.Property(u => u.UserName).HasMaxLength(128);
            builder.Property(u => u.FirstName).HasMaxLength(128);
            builder.Property(u => u.MiddleName).HasMaxLength(128);
            builder.Property(u => u.LastName).HasMaxLength(128);
            builder.Property(u => u.Email).HasMaxLength(256);

            // Each User can have many UserClaims
            builder.HasMany(e => e.Claims)
                .WithOne(e => e.User)
                .HasForeignKey(uc => uc.UserId);

            // Each User can have many entries in the UserRole join table
            builder.HasMany(e => e.UserRoles)
                .WithOne(e => e.User)
                .HasForeignKey(ur => ur.UserId);

        }
    }
}
