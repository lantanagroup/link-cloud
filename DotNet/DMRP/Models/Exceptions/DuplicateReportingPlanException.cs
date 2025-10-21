namespace LantanaGroup.Link.DMRP.Models.Exceptions
{
    /// <summary>
    /// Names the enrollment that already holds the period.
    /// </summary>
    /// <remarks>
    /// Carries the component and measure rather than the measure mapping, because those are what
    /// the unique index is keyed on. The mapping is optional, so naming it would leave the message
    /// blank for exactly the unmapped enrollment this is most likely to be reported for, and would
    /// point the caller at a field that had nothing to do with the conflict.
    /// </remarks>
    public sealed class DuplicateReportingPlanException : InvalidOperationException
    {
        public DuplicateReportingPlanException(string facilityId, string component, string measure,
            int reportingMonth, int reportingYear)
            : base($"A reporting plan already exists for facility {facilityId}, component {component}, " +
                   $"measure {measure} and period {reportingMonth}/{reportingYear}.")
        {
        }
    }
}
