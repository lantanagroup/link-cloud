using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.Shared.Application.Models.Exceptions;
public class TypeNotAllowedException : Exception
{
    public TypeNotAllowedException()
    {
    }

    public TypeNotAllowedException(string message) : base(message)
    {
    }

    public TypeNotAllowedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
