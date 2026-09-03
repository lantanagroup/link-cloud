using ServiceEnum = LantanaGroup.Link.Report.Domain.Enums.MappingIndicatorStatus;
using SdkEnum = LantanaGroup.Link.Shared.Application.Models.Integration.Report.MappingIndicatorStatus;

namespace UnitTests.LinkSdk;

/// <summary>
/// Pins the SDK's copy of the mapping indicator enum to the one the Report service actually serves.
/// </summary>
/// <remarks>
/// <para>
/// The duplication is deliberate and follows how <c>ReportingStatus</c> and <c>SubmissionStatus</c> are
/// already handled: the SDK contract is decoupled from the service's domain, so an internal refactor
/// cannot silently change what consumers see.
/// </para>
/// <para>
/// The cost of that decoupling is drift, and the values travel on the wire as integers -- so a value
/// added to one enum and not the other does not fail to compile, it silently makes the SDK report the
/// wrong state. A patient the report excludes would deserialize as whatever the SDK happens to have at
/// that ordinal. These tests are what make the duplication safe.
/// </para>
/// </remarks>
[Trait("Category", "UnitTests")]
public class MappingIndicatorStatusContractTests
{
    [Fact]
    public void BothEnumsDeclareTheSameNames()
    {
        Assert.Equal(Enum.GetNames<ServiceEnum>(), Enum.GetNames<SdkEnum>());
    }

    [Fact]
    public void EveryNameCarriesTheSameNumericValue()
    {
        // The wire format is the integer, so a name matching at a different ordinal is exactly the silent
        // failure this guards against.
        var service = Enum.GetValues<ServiceEnum>().ToDictionary(v => v.ToString(), v => (int)v);
        var sdk = Enum.GetValues<SdkEnum>().ToDictionary(v => v.ToString(), v => (int)v);

        Assert.Equal(service, sdk);
    }

    [Theory]
    [InlineData("NotEvaluated", 0)]
    [InlineData("NotApplicable", 1)]
    [InlineData("Mapped", 2)]
    [InlineData("PartiallyMapped", 3)]
    [InlineData("Unmapped", 4)]
    [InlineData("Unknown", 5)]
    [InlineData("Assumed", 6)]
    [InlineData("NothingToEvaluate", 7)]
    [InlineData("Excluded", 8)]
    public void OrdinalsArePinnedAgainstReordering(string name, int expected)
    {
        // Stored as an int in ReportEntryMappingOutcome, so reordering the members silently rewrites the
        // meaning of every row already in the database. Both enums are pinned to the same literals.
        Assert.Equal(expected, (int)Enum.Parse<ServiceEnum>(name));
        Assert.Equal(expected, (int)Enum.Parse<SdkEnum>(name));
    }
}
