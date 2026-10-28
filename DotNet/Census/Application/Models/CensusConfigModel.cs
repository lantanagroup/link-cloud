using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Census.Domain.Entities;

namespace LantanaGroup.Link.Census.Application.Models;

[BindRequired]
public class CensusConfigModel
{
    [Required] public string FacilityId { get; set; }
    [Required] public string ScheduledTrigger { get; set; }

    public bool? Enabled { get; set; } = true;

    public CensusConfigEntity ToDomain()
    {
        return new CensusConfigEntity
        {
            FacilityID = this.FacilityId,
            ScheduledTrigger = this.ScheduledTrigger,
            Enabled = this.Enabled ?? true
        };
    }

    public static CensusConfigModel FromDomain(CensusConfigEntity config)
    {
        return new CensusConfigModel
        {
            FacilityId = config.FacilityID,
            ScheduledTrigger = config.ScheduledTrigger,
            Enabled = config.Enabled
        };
    }
}