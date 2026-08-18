using LantanaGroup.Link.Automation.Link.Configuration;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Provides connection strings and EF DbContext instances for direct database
/// validation during E2E tests. Connection parameters are sourced from
/// <see cref="AutomationConfig.DatabaseConfig"/>.
/// </summary>
public class DatabaseConnectionFactory
{
    private readonly AutomationConfig.DatabaseConfig _dbConfig;

    public DatabaseConnectionFactory(AutomationConfig.DatabaseConfig dbConfig)
    {
        _dbConfig = dbConfig;
    }

    public string GetConnectionString(string database)
    {
        return $"Server=tcp:{_dbConfig.Server};Initial Catalog={database};User ID={_dbConfig.UserId};Password={_dbConfig.Password};" +
               "TrustServerCertificate=True;Encrypt=True;Connection Timeout=30;";
    }

    public static class Databases
    {
        public const string Validation = "link-validation";
    }
}
