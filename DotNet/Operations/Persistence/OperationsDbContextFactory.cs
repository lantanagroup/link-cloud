using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LantanaGroup.Link.Operations.Persistence;

/// <summary>Used only by the EF tools to build migrations. It never opens a connection.</summary>
public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    public OperationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlServer("Server=localhost;Database=link-operations;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new OperationsDbContext(options);
    }
}
