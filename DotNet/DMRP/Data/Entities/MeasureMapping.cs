using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DMRP.Data.Entities;

/// <summary>
/// Relates an NHSN measure a facility enrolls in to the digital quality measure Link evaluates
/// patients against. DMRP reports the NHSN measure only, so this is how Link translates a reporting
/// plan into something it can schedule.
/// </summary>
public class MeasureMapping : BaseEntityExtended
{
    public string Measure { get; set; } = "";
    /// <summary>
    /// The digital quality measure Link evaluates patients against, or null when Link has no dQM for
    /// this NHSN measure yet.
    /// </summary>
    /// <remarks>
    /// A null dQM is how a measure DMRP reported but Link cannot yet schedule is made visible: the
    /// sync records the measure here so it appears alongside the mappings an administrator maintains,
    /// waiting to be completed, rather than being dropped on the floor. Nothing schedules against a
    /// mapping in that state.
    /// </remarks>
    public string? DQM { get; set; }
    public Frequency Frequency { get; set; } = Frequency.Adhoc;
}
