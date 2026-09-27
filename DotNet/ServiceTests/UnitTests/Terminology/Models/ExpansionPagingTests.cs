using LantanaGroup.Link.Terminology.Application.Models;
using Xunit;

namespace UnitTests.Terminology;

/// <summary>
/// Covers <see cref="ExpansionPaging.Resolve"/>, the single place the expansion endpoints turn a
/// caller's <c>count</c>/<c>offset</c> into the page the server will actually return (LEGLINK-968).
/// </summary>
public class ExpansionPagingTests
{
    private const int Default = 20;
    private const int Max = 50;

    private static ExpansionPage Resolve(int? count, int? offset) =>
        ExpansionPaging.Resolve(count, offset, Default, Max);

    [Fact]
    public void Resolve_WithNoCount_AppliesTheConfiguredDefault()
    {
        Assert.Equal(new ExpansionPage(0, Default), Resolve(null, null));
    }

    [Fact]
    public void Resolve_WithNoOffset_StartsAtTheBeginning()
    {
        Assert.Equal(0, Resolve(5, null).Offset);
    }

    /// <summary>
    /// count=0 is the spec's "how large is this expansion?" request, so it must survive as a zero
    /// rather than being treated as an omitted parameter and replaced by the default page.
    /// </summary>
    [Fact]
    public void Resolve_WithCountZero_ReturnsZeroRatherThanTheDefault()
    {
        Assert.Equal(0, Resolve(0, null).Count);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(Default, Default)]
    [InlineData(Max, Max)]
    public void Resolve_WithCountWithinBounds_KeepsIt(int requested, int expected)
    {
        Assert.Equal(expected, Resolve(requested, null).Count);
    }

    [Theory]
    [InlineData(Max + 1)]
    [InlineData(100_000)]
    [InlineData(int.MaxValue)]
    public void Resolve_WithCountAboveTheMaximum_ClampsToIt(int requested)
    {
        Assert.Equal(Max, Resolve(requested, null).Count);
    }

    [Fact]
    public void Resolve_WithLargeOffset_KeepsIt()
    {
        // An offset past the end of the code group is not an error: it yields an empty page whose
        // expansion.total still tells the caller how far it overshot.
        Assert.Equal(int.MaxValue, Resolve(null, int.MaxValue).Offset);
    }

    [Fact]
    public void Resolve_WithNegativeCount_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Resolve(-1, null));

        Assert.Equal("The 'count' parameter cannot be negative", ex.Message);

        // No ParamName: FhirController puts this message straight into the Problem Details detail,
        // where ArgumentException's " (Parameter 'count')" suffix would be framework noise.
        Assert.Null(ex.ParamName);
    }

    [Fact]
    public void Resolve_WithNegativeOffset_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Resolve(null, -1));

        Assert.Equal("The 'offset' parameter cannot be negative", ex.Message);
        Assert.Null(ex.ParamName);
    }

    /// <summary>
    /// The count check runs first, so a request that got both wrong is told about count. Pinned only so
    /// the message a caller sees does not change silently.
    /// </summary>
    [Fact]
    public void Resolve_WithBothNegative_ReportsCount()
    {
        Assert.Equal(
            "The 'count' parameter cannot be negative",
            Assert.Throws<ArgumentException>(() => Resolve(-1, -1)).Message);
    }

    /// <summary>
    /// The two invariants every caller of Resolve depends on. Startup validation already rejects a
    /// default above the maximum, but a hand-constructed FhirService in a test does not go through it,
    /// so the clamp is applied to the default as well.
    /// </summary>
    [Theory]
    [InlineData(null, 10, 5)]
    [InlineData(7, 10, 5)]
    [InlineData(0, 10, 5)]
    [InlineData(1000, 10, 5)]
    public void Resolve_CountIsNeverNegativeAndNeverAboveTheMaximum(int? count, int defaultPageSize, int maxPageSize)
    {
        var page = ExpansionPaging.Resolve(count, null, defaultPageSize, maxPageSize);

        Assert.InRange(page.Count, 0, maxPageSize);
    }
}
