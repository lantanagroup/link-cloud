using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Submission.Data
{
    public class SubmissionContext : DbContext
    {
        public SubmissionContext(DbContextOptions<SubmissionContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.AddQuartz(builder => builder.UseSqlServer());
        }
    }

    public class SubmissionDbContextFactory : IDesignTimeDbContextFactory<SubmissionContext>
    {
        public SubmissionContext CreateDbContext(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).FullName, "Submission"))
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env}.json", optional: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<SubmissionContext>();

            var connectionString = config.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);
            optionsBuilder.UseSqlServer(connectionString);

            return new SubmissionContext(optionsBuilder.Options);
        }
    }
}
