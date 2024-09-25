using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories;

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
            _ => throw new ArgumentException("Invalid reportable event type")

        };
    }
}
