using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Services;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared;

[Trait("Category", "UnitTests")]
public class PipelineAbortRegistryTests
{
    [Fact]
    public async Task Abort_by_facility_is_visible_to_later_checks()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        var facilityId = Guid.NewGuid().ToString();

        (await registry.IsAbortedAsync(facilityId, null)).Should().BeFalse();

        await registry.AbortAsync(facilityId, reportId: null, TimeSpan.FromDays(14));

        (await registry.IsAbortedAsync(facilityId, null)).Should().BeTrue();
        (await registry.IsAbortedAsync(Guid.NewGuid().ToString(), null)).Should().BeFalse();
    }

    [Fact]
    public async Task Abort_by_report_is_visible_even_without_facility()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        var reportId = Guid.NewGuid().ToString();

        await registry.AbortAsync(facilityId: null, reportId, TimeSpan.FromDays(14));

        (await registry.IsAbortedAsync(null, reportId)).Should().BeTrue();
        (await registry.IsAbortedAsync(Guid.NewGuid().ToString(), reportId)).Should().BeTrue();
    }

    [Fact]
    public async Task Clear_removes_report_abort_without_touching_other_reports()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        var reportId = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();

        await registry.AbortAsync(null, reportId, TimeSpan.FromDays(14));
        await registry.AbortAsync(null, other, TimeSpan.FromDays(14));
        await registry.ClearAsync(null, reportId);

        (await registry.IsAbortedAsync(null, reportId)).Should().BeFalse();
        (await registry.IsAbortedAsync(null, other)).Should().BeTrue();
    }

    [Fact]
    public async Task Blank_ids_are_never_aborted()
    {
        var registry = new InMemoryPipelineAbortRegistry();
        await registry.AbortAsync(" ", " ", TimeSpan.FromDays(1));
        (await registry.IsAbortedAsync(null, null)).Should().BeFalse();
        (await registry.IsAbortedAsync("", "")).Should().BeFalse();
    }
}
