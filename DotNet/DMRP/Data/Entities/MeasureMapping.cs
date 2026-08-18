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
    public string DQM { get; set; } = "";
    public Frequency Frequency { get; set; } = Frequency.Adhoc;
}
