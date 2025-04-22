using DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
