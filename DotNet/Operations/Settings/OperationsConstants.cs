namespace LantanaGroup.Link.Operations.Settings;

public static class OperationsConstants
{
    public const string ServiceName = "Operations";

    public static class AppSettingsSectionNames
    {
        public const string ServiceInformation = "ServiceInformation";
        public const string Serilog = "Serilog";
        public const string DatabaseProvider = "DatabaseProvider";
    }

    /// <summary>
    /// "SqlServer" (the default) keeps change requests, the executor lease and migrations in the Operations database.
    /// "InMemory" is for a single local process only; nothing survives a restart and replicas do not coordinate.
    /// </summary>
    public const string InMemoryStore = "InMemory";
}
