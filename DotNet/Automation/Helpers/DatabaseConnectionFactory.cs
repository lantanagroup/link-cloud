namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Provides connection strings for direct database validation during automation.
/// </summary>
public class DatabaseConnectionFactory
{
    private readonly Configuration.AutomationConfigBase.DatabaseConfigBase _dbConfig;

    public DatabaseConnectionFactory(Configuration.AutomationConfigBase.DatabaseConfigBase dbConfig)
    {
        _dbConfig = dbConfig;
    }

    public string GetConnectionString(string database)
    {
        return $"Server=tcp:{_dbConfig.Server};Initial Catalog={database};User ID={_dbConfig.UserId};Password={_dbConfig.Password};" +
               "TrustServerCertificate=True;Encrypt=True;Connection Timeout=30;";
    }
}
