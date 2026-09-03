using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Middleware;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Xunit;

// ServiceTests globally imports Hl7.Fhir.Model, which has its own Task type.
using Task = System.Threading.Tasks.Task;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Covers the artificial response delay.
/// </summary>
/// <remarks>
/// Driven through a <see cref="FakeTimeProvider"/> rather than by sleeping, so a test for a
/// five-minute delay finishes instantly and the suite stays deterministic. A real
/// <c>Task.Delay</c> here would trade seconds of wall clock for flakiness on a loaded CI
/// agent.
/// </remarks>
public class ResponseDelayServiceTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-08-07T12:00:00Z"));

    private ResponseDelayService Service() => new(_time);

    [Fact]
    public void ByDefault_NoDelayIsConfigured()
    {
        // The state after a restart. A delay is never persisted, so this is also what a
        // deployment comes back as after a forgotten delay.
        var service = Service();

        service.Current.Should().Be(ResponseDelay.None);
        service.Current.IsActive.Should().BeFalse();
        service.Current.ConfiguredOn.Should().BeNull();
    }

    [Fact]
    public void Set_RecordsTheDelayAndWhenItWasConfigured()
    {
        var service = Service();

        var result = service.Set(2_500);

        result.Milliseconds.Should().Be(2_500);
        result.IsActive.Should().BeTrue();
        result.ConfiguredOn.Should().Be(_time.GetUtcNow());
        service.Current.Should().Be(result);
    }

    [Fact]
    public void SetToZero_ClearsRatherThanRecordingAZeroDelay()
    {
        // Zero and "no delay" are the same state, so a caller does not have to know which of
        // the two endpoints turns a delay off.
        var service = Service();
        service.Set(1_000);

        var result = service.Set(0);

        result.Should().Be(ResponseDelay.None);
        result.ConfiguredOn.Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesTheDelayAndIsSafeWhenNoneIsSet()
    {
        var service = Service();
        service.Set(1_000);

        service.Clear().Should().Be(ResponseDelay.None);
        service.Clear().Should().Be(ResponseDelay.None, "clearing twice is not an error");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ResponseDelay.MaxMilliseconds + 1)]
    [InlineData(int.MaxValue)]
    public void Set_OutsideTheAllowedRange_Throws(int milliseconds)
    {
        // The ceiling matters: without it a mistyped delay makes the contract surface
        // unusable until someone restarts the service, and there is no way to wait it out.
        var service = Service();

        var act = () => service.Set(milliseconds);

        act.Should().Throw<ArgumentOutOfRangeException>();
        service.Current.Should().Be(ResponseDelay.None, "a rejected value must not take effect");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(ResponseDelay.MaxMilliseconds)]
    public void Set_AtTheBoundaries_IsAccepted(int milliseconds)
    {
        var act = () => Service().Set(milliseconds);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ApplyAsync_WithNoDelay_ReturnsImmediately()
    {
        var service = Service();

        // No time is advanced, so this can only complete if it never waited.
        await service.ApplyAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ApplyAsync_WaitsForTheConfiguredDelay()
    {
        var service = Service();
        service.Set(30_000);

        var applying = service.ApplyAsync(CancellationToken.None);

        applying.IsCompleted.Should().BeFalse("the delay has not elapsed yet");

        _time.Advance(TimeSpan.FromMilliseconds(29_999));
        applying.IsCompleted.Should().BeFalse("one millisecond short");

        _time.Advance(TimeSpan.FromMilliseconds(1));
        await applying.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ApplyAsync_WhenTheCallerGivesUp_StopsWaiting()
    {
        // This is what keeps a long delay plus concurrent callers from tying up requests
        // nobody is waiting for any more. Without the token, each abandoned request would
        // hold on for the full delay before writing to a socket that has already closed.
        var service = Service();
        service.Set(ResponseDelay.MaxMilliseconds);

        using var cts = new CancellationTokenSource();
        var applying = service.ApplyAsync(cts.Token);

        await cts.CancelAsync();

        var act = async () => await applying.WaitAsync(TimeSpan.FromSeconds(5));
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ApplyAsync_ClearingMidWait_DoesNotShortenAWaitAlreadyUnderWay()
    {
        // The honest behaviour: an upstream that has already begun a slow response does not
        // speed up because a setting changed.
        var service = Service();
        service.Set(10_000);

        var applying = service.ApplyAsync(CancellationToken.None);
        service.Clear();

        applying.IsCompleted.Should().BeFalse();

        _time.Advance(TimeSpan.FromMilliseconds(10_000));
        await applying.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ApplyAsync_AfterClearing_ReturnsImmediatelyForNewRequests()
    {
        var service = Service();
        service.Set(ResponseDelay.MaxMilliseconds);
        service.Clear();

        await service.ApplyAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    // ------------------------------------------------------------ what it applies to

    [Theory]
    [InlineData("/msc")]
    [InlineData("/ps/annual/mrp")]
    [InlineData("/")]
    [InlineData("/some-future-contract-endpoint")]
    public void TheDelayReachesTheContractSurface(string path)
    {
        // Expressed as "everything that is not ours", so an endpoint added to the contract is
        // delayed without anyone having to remember to add it here.
        ResponseDelayMiddleware.AppliesTo(new PathString(path)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/mock-dmrp/entries")]
    [InlineData("/api/mock-dmrp/delay")]
    [InlineData("/api/mock-dmrp/entries/search")]
    [InlineData("/api/mock-dmrp/oauth2/token")]
    [InlineData("/health")]
    [InlineData("/api/mock-dmrp/info")]
    [InlineData("/swagger/index.html")]
    public void TheDelayNeverReachesTheSupportOrOperationalSurface(string path)
    {
        // Load-bearing. Delaying /api/mock-dmrp would mean a five-minute delay takes five
        // minutes to turn off, because the endpoint that clears it would be delayed too. Delaying
        // /health would push the container past its probe timeout and get it restarted,
        // which reads as an outage rather than a test in progress.
        ResponseDelayMiddleware.AppliesTo(new PathString(path)).Should().BeFalse();
    }

    [Fact]
    public void ThePathCheckIsCaseInsensitive()
    {
        ResponseDelayMiddleware.AppliesTo(new PathString("/API/Mock-Dmrp/delay")).Should().BeFalse();
        ResponseDelayMiddleware.AppliesTo(new PathString("/Health")).Should().BeFalse();
    }

    [Fact]
    public void APathThatMerelyStartsWithThoseLettersIsStillDelayed()
    {
        // StartsWithSegments, not StartsWith: a contract endpoint called /apiary would be a
        // different route from /api and must not inherit the exemption.
        ResponseDelayMiddleware.AppliesTo(new PathString("/apiary")).Should().BeTrue();
        ResponseDelayMiddleware.AppliesTo(new PathString("/healthy")).Should().BeTrue();
    }
}
