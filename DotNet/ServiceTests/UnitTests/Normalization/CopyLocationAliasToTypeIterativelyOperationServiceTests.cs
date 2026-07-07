using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class CopyLocationAliasToTypeIterativelyOperationServiceTests
{
    private readonly Mock<ILogger<CopyLocationAliasToTypeIterativelyOperationService>> _logger;
    private readonly CopyLocationAliasToTypeIterativelyOperationService _service;

    public CopyLocationAliasToTypeIterativelyOperationServiceTests()
    {
        _logger = new Mock<ILogger<CopyLocationAliasToTypeIterativelyOperationService>>();
        _service = new CopyLocationAliasToTypeIterativelyOperationService(_logger.Object);
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_AddsAliasesToLocationType_ReturnsSuccess()
    {
        var location = new Location
        {
            Id = "child-location",
            Alias = ["ICU", "Stepdown"]
        };

        var result = await _service.ProcessOperationAsync(new CopyLocationAliasToTypeIterativelyOperation(), location);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var modified = (Location)result.Resource;
        AssertAliasCode(modified, "ICU");
        AssertAliasCode(modified, "Stepdown");
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_SplitsCommaSeparatedAliases_AndTrimsValues()
    {
        var location = new Location
        {
            Id = "child-location",
            Alias = ["ICU, Stepdown,  MedSurg  "]
        };

        var result = await _service.ProcessOperationAsync(new CopyLocationAliasToTypeIterativelyOperation(), location);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var modified = (Location)result.Resource;
        AssertAliasCode(modified, "ICU");
        AssertAliasCode(modified, "Stepdown");
        AssertAliasCode(modified, "MedSurg");
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_ExistingAliasCoding_DoesNotDuplicate()
    {
        var location = new Location
        {
            Id = "child-location",
            Alias = ["ICU"],
            Type =
            [
                new CodeableConcept(CopyLocationAliasToTypeIterativelyOperationService.LocationAliasCodeSystem, "ICU")
            ]
        };

        var result = await _service.ProcessOperationAsync(new CopyLocationAliasToTypeIterativelyOperation(), location);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var modified = (Location)result.Resource;
        Assert.Single(modified.Type, concept => HasAliasCode("ICU")(concept));
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_CopiesAliasesFromParentHierarchy()
    {
        var child = new Location
        {
            Id = "child-location",
            Alias = ["Child"],
            PartOf = new ResourceReference("parent-location")
        };
        var parent = new Location
        {
            Id = "parent-location",
            Alias = ["Parent"],
            PartOf = new ResourceReference("grandparent-location")
        };
        var grandparent = new Location
        {
            Id = "grandparent-location",
            Alias = ["Grandparent"]
        };

        var result = await _service.ProcessOperationAsync(
            new CopyLocationAliasToTypeIterativelyOperation(),
            child,
            [parent, grandparent]);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        AssertAliasCode(child, "Child");
        AssertAliasCode(parent, "Parent");
        AssertAliasCode(grandparent, "Grandparent");
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_MissingParent_ReturnsSuccess_AndLogsWarning()
    {
        var location = new Location
        {
            Id = "child-location",
            Alias = ["Child"],
            PartOf = new ResourceReference("missing-parent")
        };

        var result = await _service.ProcessOperationAsync(
            new CopyLocationAliasToTypeIterativelyOperation(),
            location,
            []);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        AssertAliasCode(location, "Child");
        VerifyLog(LogLevel.Warning, "Parent location with reference missing-parent not found", Times.Once());
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_CircularParentReference_StopsAtIterationLimit_AndLogsWarning()
    {
        var locationA = new Location
        {
            Id = "location-a",
            Alias = ["A"],
            PartOf = new ResourceReference("location-b")
        };
        var locationB = new Location
        {
            Id = "location-b",
            Alias = ["B"],
            PartOf = new ResourceReference("location-a")
        };

        var result = await _service.ProcessOperationAsync(
            new CopyLocationAliasToTypeIterativelyOperation(),
            locationA,
            [locationA, locationB]);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        AssertAliasCode(locationA, "A");
        AssertAliasCode(locationB, "B");
        Assert.Single(locationA.Type, concept => HasAliasCode("A")(concept));
        Assert.Single(locationB.Type, concept => HasAliasCode("B")(concept));
        VerifyLog(LogLevel.Warning, "Maximum iteration count of 15 reached", Times.Once());
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_NonLocationResource_ReturnsFailure()
    {
        var patient = new Patient { Id = "patient-1" };

        var result = await _service.ProcessOperationAsync(new CopyLocationAliasToTypeIterativelyOperation(), patient);

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
        Assert.Contains("Resource must be a Location", result.ErrorMessage);
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_NullResource_ReturnsFailure()
    {
        var result = await _service.ProcessOperationAsync(new CopyLocationAliasToTypeIterativelyOperation(), null);

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
    }

    [Fact]
    public async Task CopyLocationAliasToTypeIteratively_NullOperation_ReturnsFailure()
    {
        var location = new Location { Id = "child-location" };

        var result = await _service.ProcessOperationAsync(null, location);

        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
    }

    private static void AssertAliasCode(Location location, string code)
    {
        Assert.Contains(location.Type, concept => HasAliasCode(code)(concept));
    }

    private static Func<CodeableConcept, bool> HasAliasCode(string code)
    {
        return concept => concept.Coding.Any(coding =>
            coding.System == CopyLocationAliasToTypeIterativelyOperationService.LocationAliasCodeSystem &&
            coding.Code == code);
    }

    private void VerifyLog(LogLevel level, string message, Times times)
    {
        _logger.Verify(
            logger => logger.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
