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
    /// <summary>
    /// The NHSN measure, or module, exactly as the DMRP API reports it in a plan's name field:
    /// "HOB", "HTCDI".
    /// </summary>
    public string Measure { get; set; } = string.Empty;

    /// <summary>
    /// The digital quality measure held by the MeasureEval service, for example
    /// "NHSNAcuteCareHospitalDailyInitialPopulation".
    /// <para>
    /// Null when the mapping is incomplete. A measure that DMRP reports but Link has never seen is
    /// inserted with no dQM and no frequency rather than dropped, so the enrollment is not lost and
    /// an administrator can complete the mapping afterwards.
    /// </para>
    /// </summary>
    public string? Dqm { get; set; }

    /// <summary>
    /// How often the measure is reported. Null on an incomplete mapping, for the same reason as
    /// <see cref="Dqm"/>.
    /// </summary>
    public Frequency? Frequency { get; set; }

    /// <summary>
    /// Whether the mapping still needs an administrator to supply the dQM and frequency.
    /// </summary>
    public bool IsIncomplete => string.IsNullOrWhiteSpace(Dqm) || Frequency is null;
}
