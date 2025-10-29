using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
public enum FhirQueryType
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

public static class FhirQueryTypeUtilities
{
    public static FhirQueryType ToDomain(string fhirQueryType)
    {
        return fhirQueryType switch
        {
            "Read" => FhirQueryType.Read,
            "Search" => FhirQueryType.Search,
            "BulkDataRequest" => FhirQueryType.BulkDataRequest,
            "BulkDataPoll" => FhirQueryType.BulkDataPoll,
            _ => throw new ArgumentOutOfRangeException(nameof(fhirQueryType), fhirQueryType, null)
        };
    }
}
