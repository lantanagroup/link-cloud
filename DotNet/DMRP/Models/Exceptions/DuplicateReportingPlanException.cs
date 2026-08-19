namespace LantanaGroup.Link.DMRP.Models.Exceptions
{
    public sealed class DuplicateReportingPlanException : InvalidOperationException
    {
        public DuplicateReportingPlanException(string facilityId, string measureMappingId, int reportingMonth, int reportingYear)
            : base($"A reporting plan already exists for facility {facilityId}, measure mapping " +
                   $"{measureMappingId} and period {reportingMonth}/{reportingYear}.")
        {
        }
    }
}
