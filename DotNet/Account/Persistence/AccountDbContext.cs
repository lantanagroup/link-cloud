using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Account.Persistence
{
    public class AccountDbContext 
        : IdentityDbContext<LinkUser, LinkRole, string, LinkUserClaim, LinkUserRole, LinkUserLogin, LinkRoleClaim, LinkUserToken>
    {
        public AccountDbContext(DbContextOptions<AccountDbContext> options) : base(options)
        {
        }       

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
