using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.MockDmrpApi.Application.Services;

namespace LantanaGroup.Link.MockDmrpApi.Application.Models;

/// <summary>
/// Request and response models for the artificial response delay.
/// </summary>
/// <remarks>
/// Part of the support surface, so deliberately absent from the contract -- the real DMRP has
/// no way to be told to answer slowly.
/// </remarks>
public class MockDelayRequest
{
    /// <summary>
    /// How long to hold each contract request, in milliseconds. Zero clears the delay.
    /// </summary>
    [Range(0, ResponseDelay.MaxMilliseconds)]
    public int Milliseconds { get; set; }
}

public class MockDelayModel
{
    public int Milliseconds { get; set; }

    /// <summary>False when no delay is configured, which is the state after a restart.</summary>
    public bool IsActive { get; set; }

    public DateTimeOffset? ConfiguredOn { get; set; }

    /// <summary>
    /// Which routes the delay reaches. Stated in the response because the scoping is the
    /// surprising part -- the support surface deliberately stays fast so the delay can always
    /// be turned off.
    /// </summary>
    public string AppliesTo { get; set; } = string.Empty;

    public static MockDelayModel From(ResponseDelay delay)
    {
        ArgumentNullException.ThrowIfNull(delay);

        return new MockDelayModel
        {
            Milliseconds = delay.Milliseconds,
            IsActive = delay.IsActive,
            ConfiguredOn = delay.ConfiguredOn,
            AppliesTo = "The contract endpoints only (GET /msc, GET /ps/annual). "
                        + "/api, /health and /swagger are never delayed."
        };
    }
}
