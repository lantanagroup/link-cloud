namespace LantanaGroup.Link.DMRP.Models.Exceptions
{
    /// <summary>
    /// A facility request carried scheduled reports while the DMRP module was enabled. With DMRP on,
    /// a facility's schedule is derived from its reporting plans, so a caller-supplied schedule is
    /// refused rather than discarded — a discarded one looks to the caller like an edit that took
    /// effect.
    /// </summary>
    public sealed class ScheduledReportsNotAcceptedException : InvalidOperationException
    {
        public ScheduledReportsNotAcceptedException(string message) : base(message)
        {
        }
    }
}
