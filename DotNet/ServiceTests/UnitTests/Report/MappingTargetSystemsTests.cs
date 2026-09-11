using LantanaGroup.Link.Report.Domain;

namespace UnitTests.Report;

/// <summary>
/// Covers which configured code map targets are recognised as HSLOC, and so decide the report's HSLOC
/// indicator.
/// </summary>
/// <remarks>
/// The target is an operator-entered string on a facility's code map, and the two failure directions are
/// not symmetric. Rejecting a target that is HSLOC reports the column as <c>NotApplicable</c> -- a claim
/// that nothing is configured for it -- and sends the operator looking for a code map that is already
/// there. Accepting one that is not silently attributes another system's mapping results to HSLOC.
/// </remarks>
[Trait("Category", "UnitTests")]
public class MappingTargetSystemsTests
{
    [Theory]
    [InlineData("https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html")]
    [InlineData("urn:oid:2.16.840.1.113883.6.259")]
    public void TheCanonicalIdentifiersAreHsloc(string targetSystem)
    {
        Assert.True(MappingTargetSystems.IsHsloc(targetSystem));
    }

    [Theory]
    [InlineData("HTTPS://WWW.CDC.GOV/NHSN/CDAPORTAL/TERMINOLOGY/CODESYSTEM/HSLOC.HTML")]
    [InlineData("https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/HSLOC.html")]
    [InlineData("URN:OID:2.16.840.1.113883.6.259")]
    [InlineData("urn:OID:2.16.840.1.113883.6.259")]
    public void CasingDoesNotChangeWhichSystemItIs(string targetSystem)
    {
        // Neither identifier has a case-sensitive part that distinguishes it from anything else, so a
        // facility that typed one of these configured HSLOC and the column has to say so.
        Assert.True(MappingTargetSystems.IsHsloc(targetSystem));
    }

    [Theory]
    [InlineData(" https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html ")]
    [InlineData("\turn:oid:2.16.840.1.113883.6.259\r\n")]
    public void SurroundingWhitespaceIsIgnored(string targetSystem)
    {
        Assert.True(MappingTargetSystems.IsHsloc(targetSystem));
    }

    [Theory]
    [InlineData("http://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html")]
    [InlineData("https://cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html")]
    [InlineData("https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html/")]
    [InlineData("urn:oid:2.16.840.1.113883.6.2599")]
    [InlineData("http://hospital.example.org/locations")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnythingElseIsNot(string? targetSystem)
    {
        // Ignoring case is as far as the leniency goes. A target differing by scheme, host, a trailing
        // slash or a digit is a different system, and a facility that entered one has a misconfiguration
        // worth surfacing rather than quietly accepting.
        Assert.False(MappingTargetSystems.IsHsloc(targetSystem));
    }
}
