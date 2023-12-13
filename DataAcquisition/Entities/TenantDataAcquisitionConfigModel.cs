using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Entities;

[BsonCollection("tenantConfig")]
public class TenantDataAcquisitionConfigModel : BaseEntity
{
    public string TenantId { get; set; }
    public List<Facility> Facilities { get; set; }
    public string? LastUpdatedBy { get; set; }
    public string? LastUpdatedDateTime { get; set; }
}

public class Facility
{
    public string FacilityId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [BsonRepresentation(BsonType.String)]
    public FhirVersion FhirVersion { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [BsonIgnoreIfNull]
    public string BaseFhirUrl { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [BsonIgnoreIfNull]
    public AuthenticationConfiguration? Auth { get; set; }
    public List<ConfigResource> ResourceSettings { get; set; }
    public string? LastUpdatedBy { get; set; }
    public string? LastUpdatedDateTime { get; set; }
}

public class TenantAuthenticationSettings
{
    public string? AuthType { get; set; }
    public string? Key { get; set; }
    public string? TokenUrl { get; set; }
    public string? Audience { get; set; }
    public string? ClientId { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
}


public class ConfigResource
{
    public List<string> ResourceType { get; set; }
    public string ConfigType { get; set; }
    [BsonIgnoreIfNull]
    public bool IsBulk { get; set; }
    public UsCoreResource UsCore { get; set; }
    public bool UseBaseAuth { get; set; }
    [BsonIgnoreIfNull]
    public AuthenticationConfiguration Auth { get; set; }
    public string? LastUpdatedBy { get; set; }
    public string? LastUpdatedDateTime { get; set; }
}

public class BaseResource
{
    
}

public class UsCoreResource : BaseResource
{
    public bool UseBaseFhirEndpoint { get; set; }
    public string BaseFhirUrl { get; set; }
    public bool UseDefaultRelativeFhirPath { get; set; }
    [BsonIgnoreIfNull]
    public string RelativeFhirPath { get; set; }
    public bool UseDefaultParameters { get; set; }
    [BsonIgnoreIfNull]
    public List<OverrideTenantParameters> Parameters { get; set; }
    public string LastUpdatedBy { get; set; }
    public string LastUpdatedDateTime { get; set; }
}

public class OverrideTenantParameters
{
    public string Name { get; set; }
    public List<string> Values { get; set; }
    public bool IsQuery { get; set; }
}

public enum ConfigType
{
    USCore
}