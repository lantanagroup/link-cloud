namespace LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;

public class MissingFacilityIdException : Exception
{
    public MissingFacilityIdException() : base("Facility ID is required to validate connection.")
    {
    }

    public MissingFacilityIdException(string message) : base(message)
    {
    }

    public MissingFacilityIdException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
