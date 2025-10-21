namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions
{
    public class ProcessingDelayException : Exception
    {
        public ProcessingDelayException(string message) : base(message) { }
    }
}
