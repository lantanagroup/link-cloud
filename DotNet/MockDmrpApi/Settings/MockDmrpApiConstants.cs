namespace LantanaGroup.Link.MockDmrpApi.Settings;

public static class MockDmrpApiConstants
{
    /// <summary>
    /// Also the Azure App Configuration label this service selects at startup.
    /// Must match the serviceMeta entry in /app-config.yaml.
    /// </summary>
    public const string ServiceName = "MockDmrpApi";

    public static class AppSettingsSectionNames
    {
        public const string MockDmrpApi = "MockDmrpApi";
    }
}
