using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests.Report;

[Trait("Category", "UnitTests")]
public class UseLatestMergeStrategyTests
{
    private static readonly string[] ProfileArrayA = ["http://example.com/oldProfile1", "http://example.com/oldProfile2"];
    private static readonly string[] ProfileArrayB = ["http://example.com/newProfile1"];

    private readonly Mock<ILogger<UseLatestStrategy>> _loggerMock;
    private readonly UseLatestStrategy _strategy;
    
    public UseLatestMergeStrategyTests()
    {
        _loggerMock = new Mock<ILogger<UseLatestStrategy>>();
        _strategy = new UseLatestStrategy(_loggerMock.Object);
    }
    
    private readonly Patient _patientV1 = new()
    {
        Id = "123",
        Meta = new Meta
        {
            Profile = [..ProfileArrayA],
            LastUpdated = DateTimeOffset.Now.AddDays(-10)
        },
        Name = [new HumanName { Family = "Smith", Given = ["John"], Use = HumanName.NameUse.Official }],
        Telecom =
        [
            new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone, Value = "555-0001",
                Use = ContactPoint.ContactPointUse.Home
            }
        ],
        BirthDate = "1980-01-01",
        Gender = AdministrativeGender.Male,
        Address =
        [
            new Address
            {
                Line = new[] { "123 Old Street" },
                City = "Oldtown",
                State = "CA",
                PostalCode = "90001"
            }
        ]
    };

    private readonly Patient _patientV2 = new()
    {
        Id = "123",
        Meta = new Meta
        {
            Profile = [..ProfileArrayB],
            LastUpdated = DateTimeOffset.Now
        },
        Name = [new HumanName { Family = "Smith", Given = ["Jonathan"], Use = HumanName.NameUse.Official }],
        Telecom =
        [
            new ContactPoint()
            {
                System = ContactPoint.ContactPointSystem.Phone, Value = "555-9999",
                Use = ContactPoint.ContactPointUse.Mobile
            }
        ],
        BirthDate = "1980-01-01", // unchanged
        Gender = AdministrativeGender.Male, // unchanged
        Address =
        [
            new Address
            {
                Line = new[] { "789 New Road" },
                City = "Newville",
                State = "NY",
                PostalCode = "10001"
            }
        ]
    };

    [Fact]
    public void MergeResources_ShouldReturnNewResourceWithMergedProfiles()
    {
        // Act
        var result = (Patient)_strategy.MergeResources(_patientV1, _patientV2);
        
        // Assert profile merge
        Assert.Contains("http://example.com/oldProfile1", result.Meta.Profile);
        Assert.Contains("http://example.com/oldProfile2", result.Meta.Profile);
        Assert.Contains("http://example.com/newProfile1", result.Meta.Profile);
        
        // Assert other properties are from _patientV2
        Assert.NotNull(result.Meta);
        Assert.NotNull(result.Meta.Profile);
        Assert.Equal("Jonathan", result.Name.First().GivenElement.First().Value);
        Assert.Equal("555-9999", result.Telecom.First().Value);
        Assert.Equal(ContactPoint.ContactPointUse.Mobile, result.Telecom.First().Use);
        Assert.Equal("789 New Road", result.Address.First().Line.First());
    }
    
    [Fact]
    public void MergeResources_DoesNotMergeMetaProfiles_WhenMergeMetaProfilesIsFalse()
    {
        // Act
        var result = (Patient)_strategy.MergeResources(_patientV1, _patientV2, mergeMetaProfiles: false);

        // Assert profiles not merged
        Assert.DoesNotContain("http://example.com/oldProfile1", result.Meta.Profile);
        Assert.DoesNotContain("http://example.com/oldProfile2", result.Meta.Profile);
        Assert.Contains("http://example.com/newProfile1", result.Meta.Profile);
        
        // Assert other properties are from _patientV2
        Assert.NotNull(result.Meta);
        Assert.Equal(_patientV2.Meta.Profile, result.Meta.Profile); // Profiles should not be merged
        Assert.Equal("Jonathan", result.Name.First().GivenElement.First().Value);
        Assert.Equal("555-9999", result.Telecom.First().Value);
        Assert.Equal(ContactPoint.ContactPointUse.Mobile, result.Telecom.First().Use);
        Assert.Equal("789 New Road", result.Address.First().Line.First());
    }
    
    [Fact]
    public void MergeResources_HandlesNullProfiles()
    {
        // Arrange
        var oldResource = new Patient { Id = "123", Meta = new Meta() };
        var newResource = new Patient { Id = "123", Meta = new Meta() };

        // Act
        var result = (Patient)_strategy.MergeResources(oldResource, newResource);

        // Assert
        Assert.NotNull(result.Meta);
        Assert.Empty(result.Meta.Profile);
    }
    
    [Fact]
    public void MergeResources_CreatesMetaIfMissing()
    {
        // Arrange
        var oldResource = new Patient
        {
            Id = "123",
            Meta = new Meta
            {
                Profile = [..ProfileArrayB]
            }
        };

        var newResource = new Patient { Id = "123", Meta = null };

        // Act
        var result = _strategy.MergeResources(oldResource, newResource);

        // Assert
        Assert.NotNull(result.Meta);
        Assert.Contains("http://example.com/newProfile1", result.Meta.Profile);
    }
    
    [Fact]
    public void MergeResources_OnMismatchedIds_LogsErrorAndReturnsOldResource()
    {
        // Arrange
        var oldResource = new Patient { Id = "old" };
        var newResource = new Patient { Id = "new", Meta = new Meta() };

        // Act
        var result = _strategy.MergeResources(oldResource, newResource);

        // Assert
        Assert.Equal("old", result.Id); // old resource should be returned
        _loggerMock.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cannot merge resources with mismatched IDs")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public void MergeResources_ThrowsIfResourcesAreNull()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => _strategy.MergeResources(null!, new Patient()));
        Assert.Throws<ArgumentNullException>(() => _strategy.MergeResources(new Patient(), null!));
    }
    
    [Fact]
    public void MergeResources_DifferentResourceTypes_LogsErrorAndReturnsOldResource()
    {
        // Arrange
        var oldResource = new Patient { Id = "123", Meta = new Meta() };
        var newResource = new Observation { Id = "456", Meta = new Meta() };

        // Act
        var result = _strategy.MergeResources(oldResource, newResource);

        // Assert
        Assert.Equal(oldResource, result); // Old resource should be returned
        _loggerMock.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cannot merge resources of different types")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}