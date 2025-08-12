using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Extensions;
public static class QueryPhaseExtensions
{
    public static QueryPhase TranslateToQueryPhase(this string queryPhaseStr)
    {
        return queryPhaseStr.ToLower() switch
        {
            "initial" => QueryPhase.Initial,
            "supplemental" => QueryPhase.Supplemental,
            "referential" => QueryPhase.Referential,
            "polling" => QueryPhase.Polling,
            "monitoring" => QueryPhase.Monitoring,
            _ => throw new ArgumentException($"Invalid value: {queryPhaseStr}")
        };
    }
}
