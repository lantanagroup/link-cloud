using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationsControllerTests
{
    [Fact]
    public async Task SaveSequence_WhenOperationIdsAreDuplicated_ReturnsBadRequest()
    {
        var operationId = Guid.NewGuid();
        var store = new Mock<INormalizationStore>();
        var sut = new NormalizationsController(store.Object);

        var model = new NormalizationSequenceDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Duplicate Operation Sequence",
            Entries =
            [
                new NormalizationSequenceEntry
                {
                    OperationId = operationId,
                    Sequence = 1
                },
                new NormalizationSequenceEntry
                {
                    OperationId = operationId,
                    Sequence = 2
                }
            ]
        };

        var result = await sut.SaveSequence(model, CancellationToken.None);

        var badRequest = result.Should()
            .BeOfType<BadRequestObjectResult>()
            .Subject;

        badRequest.StatusCode.Should().Be(400);
        badRequest.Value.Should()
            .Be("A sequence cannot contain the same operation more than once.");

        store.Verify(
            s => s.UpsertSequenceAsync(
                It.IsAny<NormalizationSequenceDefinition>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}