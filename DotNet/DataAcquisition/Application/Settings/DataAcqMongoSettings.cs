using LantanaGroup.Link.Shared.Application.Models.Configs;

namespace LantanaGroup.Link.DataAcquisition.Application.Settings;

public class DataAcqMongoSettings
{
    public MongoConnection MongoConnection { get; set; }
    public string FhirQueryConfigurationCollectionName { get; set; } = string.Empty;
    public string FhirQueryListConfigurationCollectionName { get; set; } = string.Empty;
    public string QueryPlanCollectionName { get;set; } = string.Empty;
}
