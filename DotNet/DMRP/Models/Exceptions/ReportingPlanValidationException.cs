namespace LantanaGroup.Link.DMRP.Models.Exceptions
{
    public sealed class ReportingPlanValidationException : InvalidOperationException
    {
        public ReportingPlanValidationException(string message) : base(message)
        {
        }
    }
}
