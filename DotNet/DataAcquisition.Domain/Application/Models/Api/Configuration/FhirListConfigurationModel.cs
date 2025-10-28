using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

public class FhirListConfigurationModel
{
    [DataMember]
    public string? Id { get; set; }
    [Required]
    public string FacilityId { get; set; }
    [Required]
    public string FhirBaseServerUrl { get; set; }
    public AuthenticationConfigurationModel? Authentication { get; set; }
    [Required]
    public List<EhrPatientListModel> EHRPatientLists { get; set; }
    [DataMember]
    public DateTime? CreateDate { get; set; }
    [DataMember]
    public DateTime? ModifyDate { get; set; }

    public static FhirListConfigurationModel? FromDomain(FhirListConfiguration? entity)
    {
        if (entity == null)
            return null;

        return new FhirListConfigurationModel
        {
            Id = entity.Id.ToString(),
            FacilityId = entity.FacilityId,
            FhirBaseServerUrl = entity.FhirBaseServerUrl,
            Authentication = entity.Authentication != null ? AuthenticationConfigurationModel.FromDomain(entity.Authentication) : null,
            EHRPatientLists = entity.EHRPatientLists?.Select(e => new EhrPatientListModel
            {
                FhirId = e.FhirId,
                InternalId = e.InternalId,
                Status = e.Status,
                TimeFrame = e.TimeFrame,
            }).ToList() ?? new(),
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }

    public ModelStateDictionary Validate(ModelStateDictionary? errors = default, FhirListSettings? listSettings = default)
    {
        if (errors == null)
            errors = new ModelStateDictionary();

        if (string.IsNullOrWhiteSpace(FacilityId) || string.IsNullOrWhiteSpace(FhirBaseServerUrl))
            errors.AddModelError($"{nameof(FacilityId)}-{nameof(FhirBaseServerUrl)}", $"{nameof(FacilityId)} or {nameof(FhirBaseServerUrl)} is null or empty.");

        if (string.IsNullOrWhiteSpace(FhirBaseServerUrl) || !Uri.IsWellFormedUriString(FhirBaseServerUrl, UriKind.Absolute))
            errors.AddModelError(nameof(FhirBaseServerUrl), "FhirBaseServerUrl must be a valid absolute URI.");

        if (EHRPatientLists == null || EHRPatientLists.Count != 6)
            errors.AddModelError(nameof(EHRPatientLists), "EHRPatientLists must contain exactly 6 items.");
        else
        {
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
        }

        return errors;
    }
}

public class EhrPatientListModel
{
    [Required]
    public ListType Status { get; set; }
    [Required]
    public TimeFrame TimeFrame { get; set; }
    public string? InternalId { get; set; }
    [Required]
    public string FhirId { get; set; }

    public ModelStateDictionary Validate(ModelStateDictionary? errors = default, FhirListSettings? listSettings = default)
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
