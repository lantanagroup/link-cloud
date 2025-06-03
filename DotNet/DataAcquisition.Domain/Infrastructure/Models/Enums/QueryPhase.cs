using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
public enum QueryPhase
{
    [StringValue("Initial")]
    Initial,
    [StringValue("Supplemental")]
    Supplemental,
    [StringValue("Referential")]
    Referential,
    [StringValue("Polling")]
    Polling,
    [StringValue("Monitoring")]
    Monitoring
}

public static class QueryPhaseUtilities
{
    public static QueryPhase ToDomain(string queryPlanType)
    {
        return queryPlanType.ToLower() switch
        {
            "initial" => QueryPhase.Initial,
            "supplemental" => QueryPhase.Supplemental,
            _ => throw new ArgumentOutOfRangeException(nameof(queryPlanType), queryPlanType, null)
        };
    }
}