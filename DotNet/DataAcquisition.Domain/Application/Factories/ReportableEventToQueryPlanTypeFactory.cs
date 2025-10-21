using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;

public class ReportableEventToQueryPlanTypeFactory
{
    public static Frequency GenerateQueryPlanTypeFromReportableEvent(ReportableEvent reportableEvent)
    {
        return reportableEvent switch
        {
            ReportableEvent.Discharge => Frequency.Discharge,
            ReportableEvent.EOM => Frequency.Monthly,
            ReportableEvent.EOW => Frequency.Weekly,
            ReportableEvent.EOD => Frequency.Daily,
            ReportableEvent.Adhoc => Frequency.Discharge,
            _ => throw new ArgumentException("Invalid reportable event type")

        };
    }

    public static ReportableEvent GenerateReportableEventFromQueryPlanType(Frequency queryPlanType)
    {
        return queryPlanType switch
        {
            Frequency.Discharge => ReportableEvent.Discharge,
            Frequency.Daily => ReportableEvent.EOD,
            Frequency.Weekly => ReportableEvent.EOW,
            Frequency.Monthly => ReportableEvent.EOM,
            Frequency.Adhoc => ReportableEvent.Adhoc,
            _ => throw new ArgumentException("Invalid query plan type")
        };
    }
}
