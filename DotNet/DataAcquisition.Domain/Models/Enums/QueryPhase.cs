using DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
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
        return queryPlanType switch
        {
            "Initial" => QueryPhase.Initial,
            "Supplemental" => QueryPhase.Supplemental,
            _ => throw new ArgumentOutOfRangeException(nameof(queryPlanType), queryPlanType, null)
        };
    }
}