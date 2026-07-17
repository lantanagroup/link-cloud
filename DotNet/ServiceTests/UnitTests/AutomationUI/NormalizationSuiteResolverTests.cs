using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NormalizationSuiteResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenSuiteIdIsNull_UsesDefaultSuite()
    {
        var defaultSuite = new NormalizationSuiteDefinition { Id = Guid.NewGuid(), Name = "Default" };
        var op = MakeOperation("Op1");
        defaultSuite.OperationIds.Add(op.Id);

        var store = new Mock<INormalizationStore>();
        store.Setup(s => s.GetDefaultSuiteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(defaultSuite);
        store.Setup(s => s.GetAllOperationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([op]);
        store.Setup(s => s.GetAllSequencesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var sut = new NormalizationSuiteResolver(store.Object);
        var result = await sut.ResolveAsync(null);

        result.SuiteName.Should().Be("Default");
        result.StandaloneOperations.Should().ContainSingle();
        store.Verify(s => s.GetDefaultSuiteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WhenSuiteIdProvidedAndNotFound_Throws_AndDoesNotFallbackToDefault()
    {
        var suiteId = Guid.NewGuid();
        var store = new Mock<INormalizationStore>();
        store.Setup(s => s.GetSuiteByIdAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationSuiteDefinition?)null);

        var sut = new NormalizationSuiteResolver(store.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveAsync(suiteId));

        store.Verify(s => s.GetDefaultSuiteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_WhenSuiteReferencesMissingSequence_Throws()
    {
        var suite = new NormalizationSuiteDefinition { Id = Guid.NewGuid(), Name = "Suite" };
        suite.SequenceIds.Add(Guid.NewGuid());

        var store = new Mock<INormalizationStore>();
        store.Setup(s => s.GetSuiteByIdAsync(suite.Id, It.IsAny<CancellationToken>())).ReturnsAsync(suite);
        store.Setup(s => s.GetAllOperationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(s => s.GetAllSequencesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var sut = new NormalizationSuiteResolver(store.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveAsync(suite.Id));
    }

    [Fact]
    public async Task ResolveAsync_WhenSequenceReferencesMissingOperation_Throws()
    {
        var opId = Guid.NewGuid();
        var suite = new NormalizationSuiteDefinition { Id = Guid.NewGuid(), Name = "Suite" };
        var seq = new NormalizationSequenceDefinition { Id = Guid.NewGuid(), Name = "Seq" };
        seq.Entries.Add(new NormalizationSequenceEntry { OperationId = opId, Sequence = 1 });
        suite.SequenceIds.Add(seq.Id);

        var store = new Mock<INormalizationStore>();
        store.Setup(s => s.GetSuiteByIdAsync(suite.Id, It.IsAny<CancellationToken>())).ReturnsAsync(suite);
        store.Setup(s => s.GetAllOperationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(s => s.GetAllSequencesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([seq]);

        var sut = new NormalizationSuiteResolver(store.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveAsync(suite.Id));
    }

    [Fact]
    public async Task ResolveAsync_WhenSuiteReferencesMissingStandaloneOperation_Throws()
    {
        var missingOpId = Guid.NewGuid();
        var suite = new NormalizationSuiteDefinition { Id = Guid.NewGuid(), Name = "Suite" };
        suite.OperationIds.Add(missingOpId);

        var store = new Mock<INormalizationStore>();
        store.Setup(s => s.GetSuiteByIdAsync(suite.Id, It.IsAny<CancellationToken>())).ReturnsAsync(suite);
        store.Setup(s => s.GetAllOperationsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(s => s.GetAllSequencesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var sut = new NormalizationSuiteResolver(store.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ResolveAsync(suite.Id));
    }

    private static NormalizationOperationDefinition MakeOperation(string name)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            OperationType = "CopyProperty",
            ResourceTypes = ["Location"]
        };
}
