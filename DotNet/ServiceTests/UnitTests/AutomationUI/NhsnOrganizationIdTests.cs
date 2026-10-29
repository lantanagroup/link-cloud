using System.Collections.Concurrent;
using System.Reflection;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class NhsnOrganizationIdTests
{
    [Theory]
    [InlineData("10756")]
    [InlineData("10765")]
    [InlineData(" 10760 ")]
    public void Reserved_historical_ids_are_recognized(string id)
    {
        NhsnOrganizationIds.IsReserved(id).Should().BeTrue();
        NhsnOrganizationIds.IsValid(id).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10755")]
    [InlineData("10766")]
    [InlineData("22001")]
    public void Other_ids_are_not_reserved(string? id)
    {
        NhsnOrganizationIds.IsReserved(id).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("9999")]
    [InlineData("100000")]
    [InlineData("12a45")]
    [InlineData("2200")]
    public void Invalid_ids_are_rejected(string? id)
    {
        NhsnOrganizationIds.IsValid(id).Should().BeFalse();
    }

    [Fact]
    public void Allocate_skips_reserved_and_taken_ids()
    {
        var id = NhsnOrganizationIds.Allocate(
            taken: ["22222"],
            random: new ScriptedRandom(10765, 22222, 33333));

        id.Should().Be("33333");
    }

    [Fact]
    public void Allocate_fails_when_every_draw_is_blocked()
    {
        var act = () => NhsnOrganizationIds.Allocate(
            taken: null,
            random: new ScriptedRandom(10765));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unique NHSN organization id*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("10765")]
    public void Shared_defaults_should_be_replaced(string? id)
    {
        NhsnOrganizationIdAllocator.ShouldReplace(id).Should().BeTrue();
    }

    [Fact]
    public void An_owned_id_should_be_kept()
    {
        NhsnOrganizationIdAllocator.ShouldReplace("22001").Should().BeFalse();
    }

    [Fact]
    public void Allocator_does_not_hand_out_an_id_it_just_issued()
    {
        ClearRecent();
        var first = NhsnOrganizationIdAllocator.Allocate(taken: null, random: new ScriptedRandom(44444));
        first.Should().Be("44444");

        var act = () => NhsnOrganizationIdAllocator.Allocate(taken: null, random: new ScriptedRandom(44444));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Facility_list_ids_are_read_from_a_successful_response()
    {
        var facility = new Mock<IFacilityServiceClient>();
        facility.Setup(c => c.GetFacilityListAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<Dictionary<string, string>>
            {
                StatusCode = 200,
                Body = new Dictionary<string, string> { ["22001"] = "One", [" "] = "blank" }
            });

        var ids = await NhsnOrganizationIdAllocator.FacilityIdsAsync(
            facility.Object,
            NullLogger.Instance,
            CancellationToken.None);

        ids.Should().Equal("22001");
    }

    [Theory]
    [InlineData(204)]
    [InlineData(503)]
    public async global::System.Threading.Tasks.Task Facility_list_failures_do_not_block_allocation(int statusCode)
    {
        var facility = new Mock<IFacilityServiceClient>();
        facility.Setup(c => c.GetFacilityListAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<Dictionary<string, string>>
            {
                StatusCode = statusCode,
                Body = new Dictionary<string, string> { ["22001"] = "One" }
            });

        var ids = await NhsnOrganizationIdAllocator.FacilityIdsAsync(
            facility.Object,
            NullLogger.Instance,
            CancellationToken.None);

        ids.Should().BeEmpty();
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Startup_avoids_an_org_id_that_is_already_a_facility()
    {
        ClearRecent();
        var store = new InMemoryScenarioStore();
        var user = new TestScenarioDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Needs an id",
            NhsnOrganizationId = "10765"
        };
        await store.UpsertAsync(user, CancellationToken.None);

        var facility = new Mock<IFacilityServiceClient>();
        facility.Setup(c => c.GetFacilityListAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkApiResponse<Dictionary<string, string>>
            {
                StatusCode = 200,
                Body = new Dictionary<string, string> { ["22222"] = "Existing" }
            });

        var sut = new ScenarioSeedService(store, NullLogger<ScenarioSeedService>.Instance, facility.Object)
        {
            AllocationRandom = new ScriptedRandom(22222, 33333)
        };

        await sut.StartAsync(CancellationToken.None);

        var saved = await store.GetByIdAsync(user.Id, CancellationToken.None);
        saved!.NhsnOrganizationId.Should().Be("33333");
    }

    [Fact]
    public async global::System.Threading.Tasks.Task Startup_still_reassigns_when_the_facility_list_cannot_be_read()
    {
        ClearRecent();
        var store = new InMemoryScenarioStore();
        var user = new TestScenarioDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Needs an id",
            NhsnOrganizationId = ""
        };
        await store.UpsertAsync(user, CancellationToken.None);

        var facility = new Mock<IFacilityServiceClient>();
        facility.Setup(c => c.GetFacilityListAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("tenant down"));

        var sut = new ScenarioSeedService(store, NullLogger<ScenarioSeedService>.Instance, facility.Object)
        {
            AllocationRandom = new ScriptedRandom(55555)
        };

        await sut.StartAsync(CancellationToken.None);

        var saved = await store.GetByIdAsync(user.Id, CancellationToken.None);
        saved!.NhsnOrganizationId.Should().Be("55555");
    }

    private static void ClearRecent()
    {
        var field = typeof(NhsnOrganizationIdAllocator).GetField(
            "RecentlyAllocated",
            BindingFlags.NonPublic | BindingFlags.Static);
        var recent = (ConcurrentDictionary<string, byte>)field!.GetValue(null)!;
        recent.Clear();
    }

    private sealed class ScriptedRandom : Random
    {
        private readonly int[] _values;
        private int _index;

        public ScriptedRandom(params int[] values) => _values = values;

        public override int Next(int minValue, int maxValue)
        {
            var value = _values[Math.Min(_index, _values.Length - 1)];
            _index++;
            return value;
        }
    }

    private sealed class InMemoryScenarioStore : IScenarioStore
    {
        private readonly Dictionary<Guid, TestScenarioDefinition> _items = new();

        public global::System.Threading.Tasks.Task<List<TestScenarioDefinition>> GetAllAsync(CancellationToken ct = default)
            => global::System.Threading.Tasks.Task.FromResult(_items.Values.ToList());

        public global::System.Threading.Tasks.Task<TestScenarioDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            _items.TryGetValue(id, out var scenario);
            return global::System.Threading.Tasks.Task.FromResult<TestScenarioDefinition?>(scenario);
        }

        public global::System.Threading.Tasks.Task UpsertAsync(TestScenarioDefinition scenario, CancellationToken ct = default)
        {
            _items[scenario.Id] = scenario;
            return global::System.Threading.Tasks.Task.CompletedTask;
        }

        public global::System.Threading.Tasks.Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _items.Remove(id);
            return global::System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
