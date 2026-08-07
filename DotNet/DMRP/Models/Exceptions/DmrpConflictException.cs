namespace LantanaGroup.Link.DMRP.Models.Exceptions
{
    public class DmrpConflictException : Exception
    {
        public DmrpConflictException()
        {
        }

        public DmrpConflictException(string? message) : base(message)
        {
        }

        public DmrpConflictException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
