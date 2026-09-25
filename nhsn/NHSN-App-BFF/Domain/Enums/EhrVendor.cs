namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

/// <summary>
/// The EHR vendors in scope for NHSNLink onboarding.
/// </summary>
/// <remarks>
/// This set is closed to two — <c>Other</c> and its generic-OAuth fields are deliberately not
/// implemented. Vendor is policy rather than a branch: everything that differs between Epic and
/// Cerner lives in a per-vendor profile object.
/// </remarks>
public enum EhrVendor
{
    Epic,
    Cerner
}
