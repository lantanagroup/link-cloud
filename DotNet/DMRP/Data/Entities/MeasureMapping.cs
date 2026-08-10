using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DMRP.Data.Entities;

public class MeasureMapping : BaseEntityExtended
{
    public string Measure { get; set; } = "";
    public string DQM { get; set; } = "";
    public Frequency Frequency { get; set; } = Frequency.Adhoc;
}
