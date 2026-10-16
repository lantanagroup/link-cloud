namespace LantanaGroup.Link.DMRP.Models.Exceptions
{
    /// <summary>
    /// A DMRP record addressed by a request does not exist. Kept distinct from
    /// <see cref="ApplicationException"/>, which the module uses for invalid input, so that a
    /// controller can answer 404 for a missing record and 400 for a bad one.
    /// </summary>
    public class DmrpNotFoundException : Exception
    {
        public DmrpNotFoundException()
        {
        }

        public DmrpNotFoundException(string? message) : base(message)
        {
        }

        public DmrpNotFoundException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
