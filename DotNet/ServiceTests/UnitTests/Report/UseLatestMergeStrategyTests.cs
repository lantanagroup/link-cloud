using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests.Report;

[Trait("Category", "UnitTests")]
public class UseLatestMergeStrategyTests
{
    
    private static readonly string[] ProfileArrayA = ["http://example.com/oldProfile1", "http://example.com/oldProfile2"];
    private static readonly string[] ProfileArrayB = ["http://example.com/newProfile1"];
    
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
        // Arrange
        var mockLogger = new Mock<ILogger<UseLatestStrategy>>();
        var strategy = new UseLatestStrategy(mockLogger.Object);
       

        // Act
        var result = (Patient)strategy.MergeResources(_patientV1, _patientV2);
        
        // Assert profile merge
        Assert.Contains("http://example.com/oldProfile1", result.Meta.Profile);
        Assert.Contains("http://example.com/oldProfile2", result.Meta.Profile);
        Assert.Contains("http://example.com/newProfile1", result.Meta.Profile);
        
        // Assert other properties are from _patientV2
        Assert.NotNull(result.Meta);
        Assert.NotNull(result.Meta.Profile);
        Assert.Equal("Jonathan", result.Name.First().GivenElement.First().Value);
        Assert.Equal("555-9999", result.Telecom.First().Value);
        Assert.Equal("789 New Road", result.Address.First().Line.First());
    }
}