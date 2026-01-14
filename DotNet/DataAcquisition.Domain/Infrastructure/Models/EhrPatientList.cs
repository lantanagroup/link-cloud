using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

public class EhrPatientList
{
    [Required]
    public ListType Status { get; set; }
    [Required]
    public TimeFrame TimeFrame { get; set; }
    public string? InternalId { get; set; }
    [Required]
    public string FhirId { get; set; }

    public ModelStateDictionary Validate(ModelStateDictionary? errors = default)
    {
        if (errors == null)
            errors = new ModelStateDictionary();

        if (string.IsNullOrWhiteSpace(FhirId))
        {
            errors.AddModelError(nameof(FhirId), "FhirId is required.");
        }
        
        return errors;
    }
}