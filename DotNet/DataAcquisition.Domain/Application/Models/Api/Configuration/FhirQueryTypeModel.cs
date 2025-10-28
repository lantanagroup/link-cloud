using System.Text.Json.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FhirQueryTypeModel
{
    [StringValue("Read")]
    Read,
    [StringValue("Search")]
    Search,
    [StringValue("BulkDataRequest")]
    BulkDataRequest,
    [StringValue("BulkDataPoll")]
    BulkDataPoll
}

public static class FhirQueryTypeModelUtilities
{
    public static FhirQueryTypeModel FromDomain(FhirQueryType queryType)
    {
        return queryType switch
        {
            FhirQueryType.Read => FhirQueryTypeModel.Read,
            FhirQueryType.Search => FhirQueryTypeModel.Search,
            FhirQueryType.BulkDataRequest => FhirQueryTypeModel.BulkDataRequest,
            FhirQueryType.BulkDataPoll => FhirQueryTypeModel.BulkDataPoll,
            _ => throw new Exception($"Unknown FhirQueryType: {queryType}"),
        };
    }

    public static FhirQueryType ToDomain(FhirQueryTypeModel queryType)
    {
        return queryType switch
        {
            FhirQueryTypeModel.Read => FhirQueryType.Read,
            FhirQueryTypeModel.Search => FhirQueryType.Search,
            FhirQueryTypeModel.BulkDataRequest => FhirQueryType.BulkDataRequest,
            FhirQueryTypeModel.BulkDataPoll => FhirQueryType.BulkDataPoll,
            _ => throw new Exception($"Unknown FhirQueryTypeModel: {queryType}"),
        };
    }
}
