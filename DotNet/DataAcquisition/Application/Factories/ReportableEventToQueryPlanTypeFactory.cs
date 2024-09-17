using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories;

public class ReportableEventToQueryPlanTypeFactory
{
    public static string GenerateQueryPlanTypeFromReportableEvent(ReportableEvent reportableEvent)
    {
        return reportableEvent switch
        {
            ReportableEvent.Discharge => Frequency.Discharge.GetStringValue(),
            ReportableEvent.EOM => Frequency.Monthly.GetStringValue(),
            ReportableEvent.EOW => Frequency.Weekly.GetStringValue(),
            ReportableEvent.EOD => Frequency.Daily.GetStringValue(),
            _ => null

        };
    }
}
