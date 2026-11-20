using LantanaGroup.Link.Census.Controllers;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.ServiceTests.UnitTests.Census;

public class PatientEventsLoggingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDeleteCleansLogArgumentsAndPreservesQueryAndCancellation(bool byCorrelation)
    {
        const string id = "identifier\r\n\u0085\u009bforged";
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var transaction = new Mock<IDbContextTransaction>();
        transaction.Setup(t => t.RollbackAsync(token)).Returns(Task.CompletedTask);
        var queries = new Mock<IPatientEventQueries>();
        queries.Setup(q => q.StartTransaction(token)).ReturnsAsync(transaction.Object);
        if (byCorrelation)
            queries.Setup(q => q.DeletePatientEventByCorrelationId(id, token)).ThrowsAsync(new InvalidOperationException("storage failed"));
        else
            queries.Setup(q => q.GetPatientEventById(id, token)).ThrowsAsync(new InvalidOperationException("storage failed"));
        var logger = new Mock<ILogger<PatientEventsController>>();
        var controller = new PatientEventsController(logger.Object, Mock.Of<IPatientEventManager>(), queries.Object, Mock.Of<IPatientEncounterQueries>());

        var result = byCorrelation
            ? await controller.DeletePatientEventsByCorrelation(id, token)
            : await controller.DeletePatientEvent(id, token);

        Assert.Equal(500, Assert.IsType<ObjectResult>(result).StatusCode);
        var log = Assert.Single(logger.Invocations, i => i.Method.Name == nameof(ILogger.Log));
        var message = log.Arguments[2].ToString()!;
        Assert.DoesNotContain(message, char.IsControl);
        Assert.Contains("identifier    forged", message);
        queries.VerifyAll();
        transaction.Verify(t => t.RollbackAsync(token), Times.Once);
        transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
