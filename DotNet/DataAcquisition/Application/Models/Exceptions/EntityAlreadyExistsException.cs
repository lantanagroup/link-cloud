﻿using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions
{
    [Serializable]
    internal class EntityAlreadyExistsException : Exception
    {
        public EntityAlreadyExistsException()
        {
        }

        public EntityAlreadyExistsException(string? message) : base(message)
        {
        }

        public EntityAlreadyExistsException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected EntityAlreadyExistsException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}