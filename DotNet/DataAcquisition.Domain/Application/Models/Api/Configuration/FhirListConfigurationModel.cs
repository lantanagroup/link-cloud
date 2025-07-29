using System.ComponentModel.DataAnnotations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
public class FhirListConfigurationModel
{
    [Required]
    public string FacilityId { get; set; }
    [Required]
    public string FhirBaseServerUrl { get; set; }
    public AuthenticationConfiguration? Authentication { get; set; }
    [Required]
    public List<EhrPatientListModel> EHRPatientLists { get; set; }

    public FhirListConfiguration ToDomain()
    {
        return new FhirListConfiguration
        {
            FacilityId = FacilityId,
            FhirBaseServerUrl = FhirBaseServerUrl,
            Authentication = Authentication,
            EHRPatientLists = EHRPatientLists?.Select(list => new EhrPatientList
            {
                Status = Enum.TryParse<ListType>(list.Status, true, out var status)
                    ? status
                    : throw new ArgumentException($"Invalid List Type: {list.Status}. Acceptable Values: {string.Join(',', Enum.GetValues<ListType>())}"),
                TimeFrame = Enum.TryParse<TimeFrame>(list.TimeFrame, true, out var timeFrame) 
                    ? timeFrame 
                    : throw new ArgumentException($"Invalid TimeFrame: {list.TimeFrame}. Acceptable Values: {string.Join(',', Enum.GetValues<TimeFrame>())}"),
                InternalId = list.InternalId,
                FhirId = list.FhirId
            }).ToList() ?? new List<EhrPatientList>()
        };
    }

    public static FhirListConfigurationModel FromDomain(FhirListConfiguration entity)
    {
        return new FhirListConfigurationModel
        {
            FacilityId = entity.FacilityId,
            FhirBaseServerUrl = entity.FhirBaseServerUrl,
            Authentication = entity.Authentication,
            EHRPatientLists = entity.EHRPatientLists?.Select(list => new EhrPatientListModel
            {
                Status = list.Status.ToString(),
                TimeFrame = list.TimeFrame.ToString(),
                InternalId = list.InternalId,
                FhirId = list.FhirId
            }).ToList() ?? new List<EhrPatientListModel>()
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

public class EhrPatientListModel
{
    [Required]
    public string Status { get; set; }
    [Required]
    public string TimeFrame { get; set; }
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

        //check status and timeframe against enum and settings, error message should list valid values
        if (!Enum.TryParse<ListType>(Status, true, out _))
        {
            errors.AddModelError(nameof(Status), $"Invalid Status: {Status}. Valid values are: {string.Join(", ", Enum.GetNames(typeof(ListType)))}");
        }

        if (!Enum.TryParse<TimeFrame>(TimeFrame, true, out _))
        {
            errors.AddModelError(nameof(TimeFrame), $"Invalid TimeFrame: {TimeFrame}. Valid values are: {string.Join(", ", Enum.GetNames(typeof(TimeFrame)))}");
        }

        return errors;
    }
}
