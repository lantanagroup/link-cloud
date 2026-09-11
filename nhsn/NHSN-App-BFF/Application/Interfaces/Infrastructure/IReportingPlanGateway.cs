using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

/// <summary>
/// Resolves which NHSN measures a facility can generate a report for: enrolled on its DMRP
/// reporting plan, mapped to a dQM, and that dQM has a measure-definition loaded in MeasureEval.
/// </summary>
public interface IReportingPlanGateway
{
    /// <summary>
    /// A facility whose reporting plan measures are all unsupported in MeasureEval (or has no
    /// plan at all) answers an empty list -- not an error. Zero available measures is the honest
    /// state a facility can be in, not a failure to resolve one.
    /// </summary>
    Task<IReadOnlyList<AvailableMeasure>> GetAvailableMeasuresAsync(string facilityId, CancellationToken cancellationToken = default);
}
