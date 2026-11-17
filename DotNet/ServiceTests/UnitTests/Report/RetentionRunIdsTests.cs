using LantanaGroup.Link.Report.Domain;

namespace UnitTests.Report;

/// <summary>
/// Covers the retention run id: the same firing always gives the same id.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionRunIdsTests
{
    private static readonly DateTimeOffset FireTime = new(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void For_SameFireTime_ReturnsSameId()
    {
        Assert.Equal(RetentionRunIds.For(FireTime), RetentionRunIds.For(FireTime));
    }

    [Fact]
    public void For_SameInstantWithAnotherOffset_ReturnsSameId()
    {
        var eastern = new DateTimeOffset(2026, 11, 5, 21, 0, 0, TimeSpan.FromHours(-5));

        Assert.Equal(RetentionRunIds.For(FireTime), RetentionRunIds.For(eastern));
    }

    [Fact]
    public void For_DifferentFireTime_ReturnsDifferentId()
    {
        Assert.NotEqual(RetentionRunIds.For(FireTime), RetentionRunIds.For(FireTime.AddDays(1)));
        Assert.NotEqual(RetentionRunIds.For(FireTime), RetentionRunIds.For(FireTime.AddTicks(1)));
    }

    [Fact]
    public void For_KnownFireTime_ReturnsPinnedId()
    {
        // The literal pins the namespace and the name format together; it was computed independently with
        // Python's uuid.uuid5. If this fails, recovered runs no longer group with the runs they replace.
        Assert.Equal(new Guid("088f34e4-0143-5af1-acaf-b748c2c36098"), RetentionRunIds.For(FireTime));
    }
}
