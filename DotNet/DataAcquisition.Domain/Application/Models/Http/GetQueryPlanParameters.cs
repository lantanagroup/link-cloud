using LantanaGroup.Link.Shared.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
public class GetQueryPlanParameters
{
    [Required]
    public Frequency? Type { get; set; }
}
