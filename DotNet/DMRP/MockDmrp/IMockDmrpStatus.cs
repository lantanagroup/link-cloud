namespace LantanaGroup.Link.DMRP.MockDmrp;

/// <summary>
/// Whether facility saves write through to the Mock DMRP API, decided once at startup.
/// </summary>
public interface IMockDmrpStatus
{
    /// <summary>
    /// True when <c>MockDmrpApi:Enabled</c> is true and <c>DMRP:Api:BaseUrl</c> is set. Changing either takes
    /// effect the next time Tenant starts.
    /// </summary>
    bool IsEnabled { get; }
}
