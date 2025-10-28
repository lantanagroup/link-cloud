using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Census.Domain.Entities;

namespace LantanaGroup.Link.Census.Application.Models
{
    [BindRequired]
    public class CensusConfigApiModel
    {
        [Required]
        public string FacilityId { get; set; }
        [Required]
        public string ScheduledTrigger { get; set; }
    }
}