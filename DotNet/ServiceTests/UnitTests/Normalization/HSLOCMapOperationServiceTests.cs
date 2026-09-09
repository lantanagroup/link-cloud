using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class HSLOCMapOperationServiceTests
{
    private readonly Mock<ILogger<HSLOCMapOperationService>> _logger = new();
    private readonly HSLOCMapOperationService _service;

    public HSLOCMapOperationServiceTests()
    {
        _service = new HSLOCMapOperationService(_logger.Object,
            new CodeMapOperationService(Mock.Of<ILogger<CodeMapOperationService>>()));
    }

    [Fact]
    public async Task CopiesIdentifiersAndAliasesToType()
    {
        var location = new Location
        {
            Identifier = [new Identifier("urn:local", "123")],
            Alias = [" ICU ", "Stepdown", " ", ""]
        };

        var result = await _service.ProcessOperationAsync(new HSLOCMapOperation([]), location);

        Assert.Equal(OperationStatus.NoAction, result.SuccessCode);
        Assert.Same(location, result.Resource);
        AssertCode(location, "urn:local", "123");
        AssertCode(location, HSLOCMapOperationService.LocationAliasCodeSystem, "ICU");
        AssertCode(location, HSLOCMapOperationService.LocationAliasCodeSystem, "Stepdown");
        Assert.Equal(3, location.Type.Count);
    }

    [Fact]
    public async Task CopiesFullCommaSeparatedAlias()
    {
        var location = new Location { Alias = [" ICU, Stepdown "] };

        await _service.ProcessOperationAsync(new HSLOCMapOperation([]), location);

        Assert.Single(location.Type);
        AssertCode(location, HSLOCMapOperationService.LocationAliasCodeSystem, "ICU, Stepdown");
    }

    [Fact]
    public async Task ExistingCodingsAreNotDuplicated()
    {
        var location = new Location
        {
            Alias = ["ICU", " ICU "],
            Identifier = [new Identifier("urn:local", "123"), new Identifier("urn:other", "123")],
            Type =
            [
                new CodeableConcept(HSLOCMapOperationService.LocationAliasCodeSystem, "ICU"),
                new CodeableConcept("urn:local", "123")
            ]
        };

        var result = await _service.ProcessOperationAsync(new HSLOCMapOperation([]), location);

        Assert.Equal(OperationStatus.NoAction, result.SuccessCode);
        Assert.Equal(3, location.Type.Count);
        AssertCode(location, HSLOCMapOperationService.LocationAliasCodeSystem, "ICU");
        AssertCode(location, "urn:local", "123");
        AssertCode(location, "urn:other", "123");
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("Location/parent")]
    public async Task CopiesAncestorAliasesToOriginalLocationWithoutModifyingAncestors(string reference)
    {
        var child = new Location { Id = "child", Alias = ["Child"], PartOf = new ResourceReference(reference) };
        var parent = new Location { Id = "parent", Alias = ["Parent"], PartOf = new ResourceReference("Location/grandparent") };
        var grandparent = new Location { Id = "grandparent", Alias = ["Grandparent", "Child"] };

        var result = await _service.ProcessOperationAsync(new HSLOCMapOperation([]), child, [parent, grandparent]);

        Assert.Equal(OperationStatus.NoAction, result.SuccessCode);
        Assert.Same(child, result.Resource);
        Assert.Equal(3, child.Type.Count);
        AssertCode(child, HSLOCMapOperationService.LocationAliasCodeSystem, "Child");
        AssertCode(child, HSLOCMapOperationService.LocationAliasCodeSystem, "Parent");
        AssertCode(child, HSLOCMapOperationService.LocationAliasCodeSystem, "Grandparent");
        Assert.Empty(parent.Type);
        Assert.Empty(grandparent.Type);
    }

    [Fact]
    public async Task MissingParentStillCopiesAliasAndLogsWarning()
    {
        var location = new Location { Id = "child", Alias = ["Child"], PartOf = new ResourceReference("missing-parent") };

        await _service.ProcessOperationAsync(new HSLOCMapOperation([]), location, []);

        AssertCode(location, HSLOCMapOperationService.LocationAliasCodeSystem, "Child");
        VerifyWarning("Parent location with reference missing-parent not found");
    }

    [Fact]
    public async Task CircularParentReferenceStopsAtIterationLimit()
    {
        var child = new Location { Id = "child", Alias = ["Child"], PartOf = new ResourceReference("parent") };
        var parent = new Location { Id = "parent", Alias = ["Parent"], PartOf = new ResourceReference("child") };

        await _service.ProcessOperationAsync(new HSLOCMapOperation([]), child, [child, parent]);

        Assert.Equal(2, child.Type.Count);
        AssertCode(child, HSLOCMapOperationService.LocationAliasCodeSystem, "Child");
        AssertCode(child, HSLOCMapOperationService.LocationAliasCodeSystem, "Parent");
        Assert.Empty(parent.Type);
        VerifyWarning($"Maximum iteration count of {HSLOCMapOperationService.MAX_ITERATIONS} reached");
    }

    [Fact]
    public async Task TraversalDoesNotCopyBeyondFixedIterationLimit()
    {
        var locations = Enumerable.Range(0, HSLOCMapOperationService.MAX_ITERATIONS + 1)
            .Select(index => new Location
            {
                Id = $"location-{index}",
                Alias = [$"Alias-{index}"],
                PartOf = new ResourceReference($"location-{index + 1}")
            }).ToList();

        await _service.ProcessOperationAsync(new HSLOCMapOperation([]), locations[0], locations.Cast<DomainResource>().ToList());

        Assert.Equal(HSLOCMapOperationService.MAX_ITERATIONS, locations[0].Type.Count);
        AssertCode(locations[0], HSLOCMapOperationService.LocationAliasCodeSystem, $"Alias-{HSLOCMapOperationService.MAX_ITERATIONS - 1}");
        Assert.DoesNotContain(locations[0].Type.SelectMany(concept => concept.Coding), coding => coding.Code == $"Alias-{HSLOCMapOperationService.MAX_ITERATIONS}");
        VerifyWarning($"Maximum iteration count of {HSLOCMapOperationService.MAX_ITERATIONS} reached");
    }

    [Fact]
    public async Task MapsCopiedIdentifierAndAncestorAlias()
    {
        var child = new Location { Identifier = [new Identifier("urn:local", "123")], PartOf = new ResourceReference("parent") };
        var parent = new Location { Id = "parent", Alias = ["ICU"] };
        var operation = new HSLOCMapOperation(
        [
            new CodeSystemMap("urn:local", "urn:hsloc", new Dictionary<string, CodeMap> { ["123"] = new("1027-4", "Medical critical care") }),
            new CodeSystemMap(HSLOCMapOperationService.LocationAliasCodeSystem, "urn:hsloc", new Dictionary<string, CodeMap> { ["ICU"] = new("1027-4", "Medical critical care") })
        ]);

        var result = await _service.ProcessOperationAsync(operation, child, [parent]);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        Assert.Equal(2, child.Type.Count);
        Assert.All(child.Type.SelectMany(concept => concept.Coding), coding =>
        {
            Assert.Equal("urn:hsloc", coding.System);
            Assert.Equal("1027-4", coding.Code);
            Assert.Equal("Medical critical care", coding.Display);
        });
        Assert.NotNull(result.CodeMapping);
        Assert.Empty(parent.Type);
    }

    [Fact]
    public async Task NonLocationResourceReturnsFailure()
    {
        var result = await _service.ProcessOperationAsync(new HSLOCMapOperation([]), new Patient());

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
        Assert.Contains("Resource must be a Location", result.ErrorMessage);
    }

    [Fact]
    public async Task NullResourceReturnsFailure()
    {
        var result = await _service.ProcessOperationAsync(new HSLOCMapOperation([]), null!);

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
    }

    [Fact]
    public async Task NullOperationReturnsFailure()
    {
        var result = await _service.ProcessOperationAsync(null!, new Location());

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
    }

    private static void AssertCode(Location location, string system, string code)
    {
        Assert.Single(location.Type.SelectMany(concept => concept.Coding), coding => coding.System == system && coding.Code == code);
    }

    private void VerifyWarning(string message)
    {
        _logger.Verify(logger => logger.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(message)),
            It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
    }
}