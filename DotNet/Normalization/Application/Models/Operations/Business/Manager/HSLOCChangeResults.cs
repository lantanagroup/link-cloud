using System.Diagnostics.CodeAnalysis;

namespace LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager
{
    /// <summary>
    /// What an HSLOC import changed, for the audit note that summarises it.
    /// </summary>
    /// <remarks>
    /// One import touches every code, so the audit event records these counts instead of a property
    /// change per row - see the REST API auditing ADR's answered question on the HSLOC PUT.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public readonly record struct HSLOCUpdateResult(int Updated, int Added, int Deactivated);

    /// <summary>
    /// The code and version of a deleted HSLOC row, read before the delete because the audit note
    /// names them. Null where no row matched.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record HSLOCDeletedRow(string HSLOCCode, string Version);
}
