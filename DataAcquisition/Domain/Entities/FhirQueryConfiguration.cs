﻿using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[BsonCollection("fhirQueryConfiguration")]
public class FhirQueryConfiguration : BaseEntity
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string FacilityId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FhirServerBaseUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AuthenticationConfiguration? Authentication { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? QueryPlanIds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreateDate { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ModifyDate { get; set; }
}
