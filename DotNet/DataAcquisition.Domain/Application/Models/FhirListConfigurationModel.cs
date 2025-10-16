using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class FhirListConfigurationModel : IValidatableObject
{
    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string FacilityId { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FhirBaseServerUrl { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AuthenticationConfigurationModel? Authentication { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EhrPatientList> EHRPatientLists { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime CreateDate { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ModifyDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FacilityId))
            yield return new ValidationResult("FacilityId is required.", new[] { nameof(FacilityId) });

        if (string.IsNullOrWhiteSpace(FhirBaseServerUrl))
            yield return new ValidationResult("FhirBaseServerUrl is required.", new[] { nameof(FhirBaseServerUrl) });

        if (!string.IsNullOrWhiteSpace(FhirBaseServerUrl) && !Uri.IsWellFormedUriString(FhirBaseServerUrl, UriKind.Absolute))
            yield return new ValidationResult("FhirBaseServerUrl must be a valid absolute URI.", new[] { nameof(FhirBaseServerUrl) });

        if (Authentication != null)
        {
            var authContext = new ValidationContext(Authentication, validationContext, validationContext.Items);
            foreach (var result in Authentication.Validate(authContext))
            {
                yield return result;
            }
        }
    }

    public FhirListConfiguration ToDomain()
    {
        return new FhirListConfiguration
        {
            FacilityId = this.FacilityId,
            FhirBaseServerUrl = this.FhirBaseServerUrl,
            Authentication = this.Authentication?.ToDomain(),
            EHRPatientLists = this.EHRPatientLists
        };
    }

    public static FhirListConfigurationModel? FromDomain(FhirListConfiguration? entity)
    {
        if (entity == null)
            return null;

        return new FhirListConfigurationModel
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            FhirBaseServerUrl = entity.FhirBaseServerUrl,
            Authentication = entity.Authentication != null ? AuthenticationConfigurationModel.FromDomain(entity.Authentication) : null,
            EHRPatientLists = entity.EHRPatientLists,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }
}