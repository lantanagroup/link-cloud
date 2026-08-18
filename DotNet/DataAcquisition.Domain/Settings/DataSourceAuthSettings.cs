namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

public class DataSourceAuthSettings
{
    public const string SectionName = "DataSourceAuth";

    // Temporary switch supporting rollback from secret-manager-stored PEMs to the
    // previous database-stored PEMs during the LEGLINK-14 transition. Remove once
    // all environments are confirmed on SecretManager.
    public PemKeySource KeySource { get; set; } = PemKeySource.SecretManager;
}

public enum PemKeySource
{
    SecretManager,
    Database
}
