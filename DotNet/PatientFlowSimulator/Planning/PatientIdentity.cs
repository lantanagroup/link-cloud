namespace LantanaGroup.Link.PatientFlowSimulator.Planning;

/// <summary>
/// Who a generated patient is for one month. The same cohort slot gets a new identity every month.
/// </summary>
public sealed record PatientIdentity(
    string PatientId,
    string GivenName,
    string FamilyName,
    string Gender,
    DateOnly BirthDate)
{
    /// <summary>
    /// Name as shown on census lists.
    /// </summary>
    public string DisplayName => $"{GivenName} {FamilyName}";
}
