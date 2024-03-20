﻿namespace LantanaGroup.Link.Report.Application.Error.Exceptions
{
    public class TransientException : Exception
    {
        public TransientException(string message) : base(message) { }
        public TransientException(string message, Exception? innerEx) : base(message, innerEx) { }
    }
}
