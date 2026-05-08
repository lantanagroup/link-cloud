using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace LantanaGroup.Link.Account.Persistence.Configurations
{
    public class LinkUserRoleConfiguration : IEntityTypeConfiguration<LinkUserRole>
    {
        public void Configure(EntityTypeBuilder<LinkUserRole> builder)
        {
            ConfigureTable(builder);
        }

        private void ConfigureTable(EntityTypeBuilder<LinkUserRole> builder)
        {
            builder.ToTable("UserRoles");

            builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        }
    }
}
