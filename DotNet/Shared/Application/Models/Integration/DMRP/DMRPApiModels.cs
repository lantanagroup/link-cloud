using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;

public class FacilityReportingPlanModel
{
    public string? Id { get; set; }
}

public class MeasureMappingModel
{
    public string? Id { get; set; }

    [Required]
    public string? Measure { get; set; }

    [Required]
    public string? DQM { get; set; }

    [Required]
    [EnumDataType(typeof(Frequency))]
    public Frequency? Frequency { get; set; }
}
