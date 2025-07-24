using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

public class EhrPatientList
{
    [Required]
    public PatientStatus Status { get; set; }
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

public enum TimeFrame
{
    LessThan24Hours,
    Between24To48Hours,
    MoreThan48Hours
}

public enum  PatientStatus
{
    Admitted,
    Discharged,
}