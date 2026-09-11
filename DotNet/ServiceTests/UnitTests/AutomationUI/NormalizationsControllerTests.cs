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
    public async Task SaveSuite_WhenOperationIsInSequenceAndStandalone_ReturnsBadRequest()
    {
        var operation = MakeOperation("Conditionaltransform Test");

        var sequence = MakeSequence(
            "Conditional Transform Seq",
            operation.Id);

        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Double ConditionalTransform Test",
            SequenceIds = [sequence.Id],
            OperationIds = [operation.Id]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSuiteByIdAsync(
                suite.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationSuiteDefinition?)null);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([sequence]);

        store.Setup(s => s.GetAllOperationsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([operation]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.SaveSuite(
            suite,
            CancellationToken.None);

        var badRequest = result.Should()
            .BeOfType<BadRequestObjectResult>()
            .Subject;

        badRequest.Value.Should().Be(
            "Normalization suite cannot contain the same operation more than once. " +
            "Duplicate operation(s): 'Conditionaltransform Test' " +
            "(Standalone Operations and sequence 'Conditional Transform Seq').");

        store.Verify(
            s => s.UpsertSuiteAsync(
                It.IsAny<NormalizationSuiteDefinition>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveSuite_WhenOperationIsInTwoSequences_ReturnsBadRequest()
    {
        var operation = MakeOperation("Shared Operation");

        var sequenceOne = MakeSequence(
            "Sequence One",
            operation.Id);

        var sequenceTwo = MakeSequence(
            "Sequence Two",
            operation.Id);

        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Duplicate Sequence Suite",
            SequenceIds = [sequenceOne.Id, sequenceTwo.Id]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSuiteByIdAsync(
                suite.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationSuiteDefinition?)null);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([sequenceOne, sequenceTwo]);

        store.Setup(s => s.GetAllOperationsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([operation]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.SaveSuite(
            suite,
            CancellationToken.None);

        var badRequest = result.Should()
            .BeOfType<BadRequestObjectResult>()
            .Subject;

        badRequest.Value.Should().Be(
            "Normalization suite cannot contain the same operation more than once. " +
            "Duplicate operation(s): 'Shared Operation' " +
            "(sequence 'Sequence One' and sequence 'Sequence Two').");

        store.Verify(
            s => s.UpsertSuiteAsync(
                It.IsAny<NormalizationSuiteDefinition>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveSuite_WhenStandaloneOperationIsDuplicated_ReturnsBadRequest()
    {
        var operation = MakeOperation("Duplicate Standalone Operation");

        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Duplicate Standalone Suite",
            OperationIds = [operation.Id, operation.Id]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSuiteByIdAsync(
                suite.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationSuiteDefinition?)null);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        store.Setup(s => s.GetAllOperationsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([operation]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.SaveSuite(
            suite,
            CancellationToken.None);

        var badRequest = result.Should()
            .BeOfType<BadRequestObjectResult>()
            .Subject;

        badRequest.Value.Should().Be(
            "Normalization suite cannot contain the same operation more than once. " +
            "Duplicate operation(s): 'Duplicate Standalone Operation' " +
            "(Standalone Operations).");

        store.Verify(
            s => s.UpsertSuiteAsync(
                It.IsAny<NormalizationSuiteDefinition>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveSuite_WhenOperationsAreUnique_SavesSuite()
    {
        var sequenceOperation = MakeOperation("Sequence Operation");
        var standaloneOperation = MakeOperation("Standalone Operation");

        var sequence = MakeSequence(
            "Test Sequence",
            sequenceOperation.Id);

        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Valid Suite",
            SequenceIds = [sequence.Id],
            OperationIds = [standaloneOperation.Id]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSuiteByIdAsync(
                suite.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationSuiteDefinition?)null);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([sequence]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.SaveSuite(
            suite,
            CancellationToken.None);

        result.Should().BeOfType<JsonResult>();

        store.Verify(
            s => s.UpsertSuiteAsync(
                suite,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

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

        var result = await sut.SaveSequence(
            model,
            CancellationToken.None);

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

    [Fact]
    public async Task SaveSuite_WhenSameSequenceIsSelectedTwice_ReturnsBadRequest()
    {
        var operation = MakeOperation("Shared Operation");

        var sequence = MakeSequence(
            "Repeated Sequence",
            operation.Id);

        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Duplicate Sequence Selection Suite",
            SequenceIds = [sequence.Id, sequence.Id]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSuiteByIdAsync(
                suite.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationSuiteDefinition?)null);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([sequence]);

        store.Setup(s => s.GetAllOperationsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([operation]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.SaveSuite(
            suite,
            CancellationToken.None);

        result.Should()
            .BeOfType<BadRequestObjectResult>();

        store.Verify(
            s => s.UpsertSuiteAsync(
                It.IsAny<NormalizationSuiteDefinition>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CloneSuite_WhenSourceContainsDuplicateOperations_ReturnsBadRequest()
    {
        var operation = MakeOperation("Shared Operation");

        var sequence = MakeSequence(
            "Sequence One",
            operation.Id);

        var sourceSuite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Invalid Source Suite",
            SequenceIds = [sequence.Id],
            OperationIds = [operation.Id]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSuiteByIdAsync(
                sourceSuite.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceSuite);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([sequence]);

        store.Setup(s => s.GetAllOperationsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([operation]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.CloneSuite(
            new IdRequest { Id = sourceSuite.Id },
            CancellationToken.None);

        result.Should()
            .BeOfType<BadRequestObjectResult>();

        store.Verify(
            s => s.UpsertSuiteAsync(
                It.IsAny<NormalizationSuiteDefinition>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveSequence_WhenUpdateWouldDuplicateStandaloneOperationInReferencingSuite_ReturnsBadRequest()
    {
        var existingSequenceOperation =
            MakeOperation("Existing Sequence Operation");

        var standaloneOperation =
            MakeOperation("Standalone Operation");

        var sequence = MakeSequence(
            "Sequence One",
            existingSequenceOperation.Id);

        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Referencing Suite",
            SequenceIds = [sequence.Id],
            OperationIds = [standaloneOperation.Id]
        };

        var updatedSequence = new NormalizationSequenceDefinition
        {
            Id = sequence.Id,
            Name = sequence.Name,
            Entries =
            [
                new NormalizationSequenceEntry
            {
                OperationId = existingSequenceOperation.Id,
                Sequence = 1
            },
            new NormalizationSequenceEntry
            {
                OperationId = standaloneOperation.Id,
                Sequence = 2
            }
            ]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSequenceByIdAsync(
                sequence.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sequence);

        store.Setup(s => s.GetAllSuitesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([suite]);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([sequence]);

        store.Setup(s => s.GetAllOperationsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                existingSequenceOperation,
            standaloneOperation
            ]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.SaveSequence(
            updatedSequence,
            CancellationToken.None);

        var badRequest = result.Should()
            .BeOfType<BadRequestObjectResult>()
            .Subject;

        badRequest.Value.Should()
            .Be(
                "Saving this sequence would make normalization suite " +
                "'Referencing Suite' invalid. " +
                "Normalization suite cannot contain the same operation more than once. " +
                "Duplicate operation(s): 'Standalone Operation' " +
                "(Standalone Operations and sequence 'Sequence One').");

        store.Verify(
            s => s.UpsertSequenceAsync(
                It.IsAny<NormalizationSequenceDefinition>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveSequence_WhenUpdateKeepsReferencingSuiteValid_SavesSequence()
    {
        var sequenceOperation =
            MakeOperation("Sequence Operation");

        var standaloneOperation =
            MakeOperation("Standalone Operation");

        var sequence = MakeSequence(
            "Sequence One",
            sequenceOperation.Id);

        var suite = new NormalizationSuiteDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Valid Suite",
            SequenceIds = [sequence.Id],
            OperationIds = [standaloneOperation.Id]
        };

        var updatedSequence = new NormalizationSequenceDefinition
        {
            Id = sequence.Id,
            Name = sequence.Name,
            Entries =
            [
                new NormalizationSequenceEntry
            {
                OperationId = sequenceOperation.Id,
                Sequence = 1
            }
            ]
        };

        var store = new Mock<INormalizationStore>();

        store.Setup(s => s.GetSequenceByIdAsync(
                sequence.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sequence);

        store.Setup(s => s.GetAllSuitesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([suite]);

        store.Setup(s => s.GetAllSequencesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([sequence]);

        var sut = new NormalizationsController(store.Object);

        var result = await sut.SaveSequence(
            updatedSequence,
            CancellationToken.None);

        result.Should()
            .BeOfType<JsonResult>();

        store.Verify(
            s => s.UpsertSequenceAsync(
                updatedSequence,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static NormalizationOperationDefinition MakeOperation(string name)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            OperationType = "ConditionalTransform",
            ResourceTypes = ["Encounter"]
        };

    private static NormalizationSequenceDefinition MakeSequence(
        string name,
        Guid operationId)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Entries =
            [
                new NormalizationSequenceEntry
                {
                    OperationId = operationId,
                    Sequence = 1
                }
            ]
        };
}