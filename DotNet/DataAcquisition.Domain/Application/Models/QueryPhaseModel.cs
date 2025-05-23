using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QueryPhaseModel
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

public static class QueryPhaseModelUtilities
{
    public static QueryPhaseModel FromDomain(QueryPhase queryPhase)
    {
        return queryPhase switch
        {
            QueryPhase.Initial => QueryPhaseModel.Initial,
            QueryPhase.Supplemental => QueryPhaseModel.Supplemental,
            QueryPhase.Referential => QueryPhaseModel.Referential,
            QueryPhase.Polling => QueryPhaseModel.Polling,
            QueryPhase.Monitoring => QueryPhaseModel.Monitoring,
            _ => throw new Exception($"Unknown QueryPhase: {queryPhase}"),
        };
    }

    public static QueryPhase ToDomain(QueryPhaseModel queryPhase)
    {
        return queryPhase switch
        {
            QueryPhaseModel.Initial => QueryPhase.Initial,
            QueryPhaseModel.Supplemental => QueryPhase.Supplemental,
            QueryPhaseModel.Referential => QueryPhase.Referential,
            QueryPhaseModel.Polling => QueryPhase.Polling,
            QueryPhaseModel.Monitoring => QueryPhase.Monitoring,
            _ => throw new Exception($"Unknown QueryPhaseModel: {queryPhase}"),
        };
    }

}
