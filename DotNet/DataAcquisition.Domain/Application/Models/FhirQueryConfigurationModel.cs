using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[DataContract]
public class FhirQueryConfigurationModel
{
    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Id { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? FacilityId { get; set; }

    [DataMember]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FhirServerBaseUrl { get; set; }

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

    public DateTime? CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }

    public FhirQueryConfiguration ToDomain()
    {
        return new FhirQueryConfiguration
        {
            FacilityId = this.FacilityId,
            FhirServerBaseUrl = this.FhirServerBaseUrl,
            Authentication = this.Authentication?.ToDomain(),
            MaxConcurrentRequests = this.MaxConcurrentRequests,
            MinAcquisitionPullTime = this.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = this.MaxAcquisitionPullTime
        };
    }

    public static FhirQueryConfigurationModel FromDomain(FhirQueryConfiguration? entity)
    {
        if (entity == null)
            return null;

        return new FhirQueryConfigurationModel
        {
            Id = entity.Id.ToString(),
            FacilityId = entity.FacilityId,
            FhirServerBaseUrl = entity.FhirServerBaseUrl,
            Authentication = entity.Authentication != null ? AuthenticationConfigurationModel.FromDomain(entity.Authentication) : null,
            MaxConcurrentRequests = entity.MaxConcurrentRequests,
            MinAcquisitionPullTime = entity.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = entity.MaxAcquisitionPullTime,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }
}