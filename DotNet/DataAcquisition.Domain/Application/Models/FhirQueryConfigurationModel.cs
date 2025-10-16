using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class FhirQueryConfigurationModel : IValidatableObject
{
    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string FacilityId { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FhirServerBaseUrl { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AuthenticationConfigurationModel? Authentication { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxConcurrentRequests { get; set; } = 8;

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan? MinAcquisitionPullTime { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(TimeSpanConverter))]
    public TimeSpan? MaxAcquisitionPullTime { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeZone { get; set; } = null;

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

        if (Authentication != null)
        {
            var authContext = new ValidationContext(Authentication, validationContext, validationContext.Items);
            foreach (var result in Authentication.Validate(authContext))
            {
                yield return result;
            }
        }
    }

    public FhirQueryConfiguration ToDomain()
    {
        return new FhirQueryConfiguration
        {
            FacilityId = this.FacilityId,
            FhirServerBaseUrl = this.FhirServerBaseUrl,
            Authentication = this.Authentication?.ToDomain(),
            MaxConcurrentRequests = this.MaxConcurrentRequests,
            MinAcquisitionPullTime = this.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = this.MaxAcquisitionPullTime,
            TimeZone = this.TimeZone
        };
    }

    public static FhirQueryConfigurationModel FromDomain(FhirQueryConfiguration? entity)
    {
        if (entity == null)
            return null;

        return new FhirQueryConfigurationModel
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            FhirServerBaseUrl = entity.FhirServerBaseUrl,
            Authentication = entity.Authentication != null ? AuthenticationConfigurationModel.FromDomain(entity.Authentication) : null,
            MaxConcurrentRequests = entity.MaxConcurrentRequests,
            MinAcquisitionPullTime = entity.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = entity.MaxAcquisitionPullTime,
            TimeZone = entity.TimeZone,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }
}