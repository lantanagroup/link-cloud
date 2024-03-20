﻿namespace LantanaGroup.Link.Report.Application.Error.Exceptions
{
    public class TerminatingException : Exception
    {
        public TerminatingException(string message) : base(message) { }
        public TerminatingException(string message, Exception? innerEx) : base(message, innerEx) { }
    }
}
