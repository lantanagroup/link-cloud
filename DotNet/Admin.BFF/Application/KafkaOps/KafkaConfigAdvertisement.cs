namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

/// <summary>
/// The partition guard treats a consumer as safe when its client id ends in -c{version}.
/// That suffix is how a member shows it is running earliest offset reset and a fast
/// metadata refresh. This console does not change consumer configuration itself.
/// </summary>
public static class KafkaConfigAdvertisement
{
    public static bool ClientAdvertisesExpectedConfig(string? clientId, int expectedVersion)
    {
        if (string.IsNullOrWhiteSpace(clientId) || expectedVersion <= 0)
            return false;

        return clientId.EndsWith("-c" + expectedVersion, StringComparison.Ordinal);
    }
}
