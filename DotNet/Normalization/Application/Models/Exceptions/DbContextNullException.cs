namespace LantanaGroup.Link.Normalization.Application.Models.Exceptions;

public class DbContextNullException : Exception
{
    public DbContextNullException() : base()
    {
    }

    public DbContextNullException(string message) : base(message)
    {
    }
}
