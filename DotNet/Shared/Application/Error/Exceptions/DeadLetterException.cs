using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Error.Exceptions
{
    public class DeadLetterException : Exception
    {
        public DeadLetterException(string message) : base(message)
        {
        }

        public DeadLetterException(string message, Exception innerEx) : base(message, innerEx)
        {
            
        }
    }
}
