namespace LantanaGroup.Link.DMRP.Models;

/// <summary>
/// Whether DMRP is enabled in this deployment, and whether facility saves write through to the Mock DMRP API.
/// </summary>
public class DmrpStatusModel
{
    /// <summary>
    /// Tenant's <c>DMRP:Enabled</c>. When true, a facility's scheduled reports come from its DMRP reporting
    /// plans rather than from the request.
    /// </summary>
    public bool DmrpEnabled { get; set; }

    /// <summary>
    /// True when DMRP is enabled and the Mock DMRP API is switched on (<c>MockDmrpApi:Enabled</c>), so the
    /// facility form's report pickers are written to the mock as enrollment.
    /// </summary>
    public bool MockDmrpEnabled { get; set; }
}
