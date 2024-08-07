﻿using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("fhirQueryConfiguration")]
public class FhirQueryConfiguration : BaseEntityExtended
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string FacilityId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FhirServerBaseUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [BsonIgnoreIfNull]
    public AuthenticationConfiguration? Authentication { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? QueryPlanIds { get; set; }
}
