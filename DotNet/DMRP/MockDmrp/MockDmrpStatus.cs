using LantanaGroup.Link.DMRP.Config;

namespace LantanaGroup.Link.DMRP.MockDmrp;

/// <summary>
/// Reads the Mock DMRP API's own switch, <c>MockDmrpApi:Enabled</c>, so Tenant and the mock never disagree
/// about whether the mock is on.
/// </summary>
/// <remarks>
/// The row is unlabeled in App Configuration so both services see it, and it exists only where the mock is
/// deployed. A production store never carries it, so the write-through cannot turn on there, and the Link
/// token is never sent to the real DMRP API.
/// </remarks>
public sealed class MockDmrpStatus : IMockDmrpStatus
{
    /// <summary>
    /// The key the mock itself reads.
    /// </summary>
    public const string EnabledConfigurationKey = "MockDmrpApi:Enabled";

    /// <summary>
    /// Creates a status with a fixed answer.
    /// </summary>
    public MockDmrpStatus(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    /// <inheritdoc />
    public bool IsEnabled { get; }

    /// <summary>
    /// Reads the switch the same way the mock does: anything but a boolean true is off, so a mistyped value
    /// leaves Tenant behaving as it does with the real DMRP rather than failing to start. Off as well when
    /// <c>DMRP:Api:BaseUrl</c> is not set, since there would be nowhere to write.
    /// </summary>
    public static MockDmrpStatus FromConfiguration(IConfiguration configuration, DmrpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(settings);

        var switchedOn = bool.TryParse(configuration[EnabledConfigurationKey], out var enabled) && enabled;

        return new MockDmrpStatus(switchedOn && !string.IsNullOrWhiteSpace(settings.Api.BaseUrl));
    }
}
