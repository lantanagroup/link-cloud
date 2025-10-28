using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[DataContract]
[Table("fhirListConfiguration")]
public class FhirListConfiguration : BaseEntityExtended
{
    [Required]
    [DataMember]
    public string FacilityId { get; set; }
    [Required]
    [DataMember]
    public string FhirBaseServerUrl { get; set; }
    [DataMember]
    public AuthenticationConfiguration? Authentication { get; set; }
    [DataMember]
    public List<EhrPatientList> EHRPatientLists { get; set; }

    public ModelStateDictionary Validate(ModelStateDictionary? errors = default)
    {
        if (errors == null)
            errors = new ModelStateDictionary();

        if (string.IsNullOrWhiteSpace(FacilityId) || string.IsNullOrWhiteSpace(FhirBaseServerUrl))
            errors.AddModelError($"{nameof(FacilityId)}-{nameof(FhirBaseServerUrl)}", $"{nameof(FacilityId)} or {nameof(FhirBaseServerUrl)} is null or empty.");

        if (string.IsNullOrWhiteSpace(FhirBaseServerUrl) || !Uri.IsWellFormedUriString(FhirBaseServerUrl, UriKind.Absolute))
            errors.AddModelError(nameof(FhirBaseServerUrl), "FhirBaseServerUrl must be a valid absolute URI.");

        if (EHRPatientLists == null || EHRPatientLists.Count != 6)
            errors.AddModelError(nameof(EHRPatientLists), "EHRPatientLists must contain exactly 6 items.");

        //ensure that only one type TimeFrame and Status combination of patient list is present based
        var uniqueLists = new HashSet<string>();

        foreach (var list in EHRPatientLists)
        {
            errors = list.Validate(errors);

            string uniqueKey = $"{list.TimeFrame}-{list.Status}";
            if (!uniqueLists.Add(uniqueKey))
            {
                // Duplicate TimeFrame and Status combination found
                errors.AddModelError(nameof(EHRPatientLists), $"Duplicate TimeFrame and Status combination found: {uniqueKey}");
            }
        }
        return errors;  
    }
}
