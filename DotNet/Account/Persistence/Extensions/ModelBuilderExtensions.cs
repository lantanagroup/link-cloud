using LantanaGroup.Link.Account.Domain.Entities;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Account.Persistence.Extensions
{
    public static class ModelBuilderExtensions
    {
        public static void SeedRoles(this ModelBuilder modelBuilder)
        {
            // Seed data
            LinkRole userRole = new("LinkUser")
            {
                Description = "A user of the Link application",
                CreatedOn = DateTime.UtcNow
            };

            LinkRoleClaim linkRoleClaim = new()
            {
                Id = 1,
                RoleId = userRole.Id,
                ClaimType = LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions,
                ClaimValue = nameof(LinkSystemPermissions.IsLinkAdmin)
            };            



            modelBuilder.Entity<LinkRole>().HasData(userRole);
            modelBuilder.Entity<LinkRoleClaim>().HasData(linkRoleClaim);          
            
        }
    }
}
