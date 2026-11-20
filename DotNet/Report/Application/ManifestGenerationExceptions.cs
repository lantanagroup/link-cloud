namespace LantanaGroup.Link.Report.Application
{
    /// <summary>
    /// Tenant answered but holds no facility configuration for the schedule's facility. Derives from
    /// <see cref="InvalidOperationException"/>, which is what the Tenant client throws for a 404, so
    /// callers that already handled that type behave the same.
    /// </summary>
    public class FacilityConfigNotFoundException : InvalidOperationException
    {
        public FacilityConfigNotFoundException(string message, Exception? innerException = null)
            : base(message, innerException) { }
    }

    /// <summary>
    /// The facility configuration could not be fetched from Tenant: the service failed, could not be
    /// reached, or the client is misconfigured.
    /// </summary>
    public class TenantUnavailableException : Exception
    {
        public TenantUnavailableException(string message, Exception? innerException = null)
            : base(message, innerException) { }
    }
}
