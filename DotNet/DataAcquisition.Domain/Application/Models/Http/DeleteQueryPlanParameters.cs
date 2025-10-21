using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
public class DeleteQueryPlanParameters
{
    [BindRequired]
    [Required]
    public Frequency? Type { get; set; }
}
