using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using System.Net;

namespace ServiceTests.UnitTests;

[Trait("Category", "UnitTests")]
public class OpOutcomeExceptionTests
{
    [Fact]
    public void Constructor_ShouldPreserveInnerException()
    {
        // Arrange
        var innerException = new FhirOperationException("Inner message", HttpStatusCode.BadRequest);
        var message = "Outer message";

        // Act
        var exception = new OpOutcomeException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException.Status, exception.Status);
        Assert.Equal(innerException.Outcome, exception.Outcome);

        Assert.NotNull(exception.InnerException);
        Assert.Same(innerException, exception.InnerException);
        Assert.Contains("Inner message", exception.StackTrace);
    }
}
