using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Shared.Application.Error.Exceptions
{
    public class TransientException : Exception
    {
        public TransientException(string message) : base(message)
        {
          
        }

        public TransientException(string message, Exception? innerEx) : base(message, innerEx)
        {
            
        }
    }
}
