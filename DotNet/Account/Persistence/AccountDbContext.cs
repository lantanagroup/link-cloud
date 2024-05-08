using LantanaGroup.Link.Account.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Account.Persistence
{
    public class AccountDbContext : DbContext
    { 
        
        public AccountDbContext(DbContextOptions<AccountDbContext> options) : base(options)
        {
        }       

        public DbSet<LinkUser> Users { get; set; } = null!;
        public DbSet<LinkRole> Roles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {           
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
