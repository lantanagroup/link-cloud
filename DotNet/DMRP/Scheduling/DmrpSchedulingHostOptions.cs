namespace LantanaGroup.Link.DMRP.Scheduling
{
    /// <summary>
    /// What the module needs to know about the host's own scheduling to take it over.
    /// </summary>
    /// <param name="ClassicJobGroup">
    /// The Quartz job group the host's own, pre-DMRP report jobs live in. The module deletes that
    /// group at boot when it is enabled, so jobs left from before the flag cannot fire against a
    /// measure list the module no longer maintains. The host passes it in - the module has no way to
    /// know a name that belongs to whichever service hosts it.
    /// </param>
    public sealed record DmrpSchedulingHostOptions(string ClassicJobGroup);
}
