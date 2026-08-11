namespace LantanaGroup.Link.MockDmrpApi.Settings;

public static class DmrpApiConstants
{
    /// <summary>
    /// Also the Azure App Configuration label this service selects at startup.
    /// Must match the serviceMeta entry in /app-config.yaml.
    /// </summary>
    /// <remarks>
    /// "Mock" is deliberate here and in the configuration section name: this identifies a
    /// deployed stand-in, and an operator reading a config store should see that it is not
    /// the real DMRP API. Only the C# type names drop the prefix, so that "Mock" keeps its
    /// usual meaning of a test double.
    /// </remarks>
    public const string ServiceName = "MockDmrpApi";
}
