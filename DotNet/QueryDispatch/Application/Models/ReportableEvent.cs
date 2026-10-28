using LantanaGroup.Link.Shared.Application.Utilities;

namespace QueryDispatch.Application.Models;

public enum ReportableEvents
{
    [StringValue("Discharge")]
    Discharge,
    [StringValue("Adhoc")]
    Adhoc
}