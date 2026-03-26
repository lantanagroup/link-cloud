using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Persistence.Extensions;
using Link.Authorization.Infrastructure;
using Link.Authorization.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Reflection.Emit;

namespace LantanaGroup.Link.Account.Persistence;

public class AccountDbContext : DbContext
{ 
    
    public AccountDbContext() : base()
    {
    }

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

    public class AccountDbContextFactory : IDesignTimeDbContextFactory<AccountDbContext>
    {
        public AccountDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AccountDbContext>();
            optionsBuilder.UseSqlServer();

            return new AccountDbContext(optionsBuilder.Options);
        }
    }
}

//public class AccountDbContextFactory : IDesignTimeDbContextFactory<AccountDbContext>
//{
//    public AccountDbContext CreateDbContext(string[] args)
//    {
//        string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

//        IConfiguration config = new ConfigurationBuilder()
//            .SetBasePath(Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "Account"))
//            .AddJsonFile("appsettings.json")
//            .AddJsonFile($"appsettings.{env}.json", optional: true)
//            //.AddEnvironmentVariables()
//            .Build();

//        var optionsBuilder = new DbContextOptionsBuilder<AccountDbContext>();
//        var connectionString = config.GetConnectionString("SqlServer");
//        connectionString = string.IsNullOrWhiteSpace(connectionString) ? "Data Source;Initial;Integrated Security=True;" : connectionString;
//        optionsBuilder.UseSqlServer(connectionString);

//        return new AccountDbContext(optionsBuilder.Options);
//    }
//}
