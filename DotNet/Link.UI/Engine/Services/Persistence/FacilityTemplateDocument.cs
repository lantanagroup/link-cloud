using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Automation.UI.Services.Persistence;

internal sealed class FacilityTemplateDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsDefault { get; set; }
    public string? VendorName { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? QueryPlanTemplateId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? NormalizationSuiteId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? OrganizationResourceMapTemplateId { get; set; }

    public bool EnableOrganizationLocationMapping { get; set; }

    public List<string> AllowedPatientConfigurationIds { get; set; } = [];
    public bool AllowPatientConfigurationsOutsideSet { get; set; } = true;

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset UpdatedAt { get; set; }
}
