namespace LantanaGroup.Link.Census.Application.Models;

public class MissingFacilityIdException : Exception
{
    public MissingFacilityIdException(string message) : base(message) { }

    public MissingFacilityIdException(string message, Exception innerException) : base(message, innerException) { }

    public MissingFacilityIdException() { }
}
