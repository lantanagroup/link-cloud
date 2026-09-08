using LantanaGroup.Link.Shared.Application.Models.Terminology;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Code = LantanaGroup.Link.Terminology.Application.Models.Code;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Terminology;

/// <summary>
/// Covers <see cref="CodeSearchService"/>: scope resolution, matching, de-duplication, ordering, status
/// and paging for <c>GET /api/terminology/codes</c>.
/// </summary>
/// <remarks>
/// The cache is mocked at <see cref="ICodeGroupCacheService"/> and the <see cref="FhirService"/> is real
/// and shares that same mock, so the status these tests observe is resolved by the same code
/// <c>$validate-code</c> uses rather than by a stub that could drift from it (LEGLINK-889).
///
/// <c>GetAllCodeGroups</c> is stubbed with a factory rather than a fixed list because the service appends
/// the value sets to the list the cache handed back; the real cache builds a fresh list per call.
/// </remarks>
public class CodeSearchServiceTests
{
    private const string ActCode = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
    private const string Hsloc = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    private const string EncounterValueSet = "http://terminology.hl7.org/ValueSet/v3-ActEncounterCode";

    private readonly Mock<ICodeGroupCacheService> _cache = new();
    private readonly CodeSearchService _service;

    public CodeSearchServiceTests()
    {
        var fhirService = new FhirService(_cache.Object, Mock.Of<ILogger<FhirService>>());
        _service = new CodeSearchService(_cache.Object, fhirService);
    }

    private static CodeGroup CodeSystemGroup(string url, string version, params Code[] codes) => new()
    {
        Id = url,
        Url = url,
        Version = version,
        Type = CodeGroup.CodeGroupTypes.CodeSystem,
        Codes = new Dictionary<string, List<Code>> { { url, codes.ToList() } }
    };

    private static CodeGroup ValueSetGroup(string url, string memberSystem, params Code[] codes) => new()
    {
        Id = url,
        Url = url,
        Version = "1.0.0",
        Type = CodeGroup.CodeGroupTypes.ValueSet,
        Codes = new Dictionary<string, List<Code>> { { memberSystem, codes.ToList() } }
    };

    private void GivenCodeSystem(CodeGroup group) =>
        _cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.CodeSystem, group.Url!, It.IsAny<string>()))
            .Returns(group);

    private void GivenValueSet(CodeGroup group) =>
        _cache.Setup(x => x.GetCodeGroup(CodeGroup.CodeGroupTypes.ValueSet, group.Url!, It.IsAny<string>()))
            .Returns(group);

    private void GivenAllContent(CodeGroup[] codeSystems, CodeGroup[] valueSets)
    {
        _cache.Setup(x => x.GetAllCodeGroups(CodeGroup.CodeGroupTypes.CodeSystem))
            .Returns(() => codeSystems.ToList());
        _cache.Setup(x => x.GetAllCodeGroups(CodeGroup.CodeGroupTypes.ValueSet))
            .Returns(() => valueSets.ToList());
    }

    // ---------------------------------------------------------------- matching

    [Fact]
    public async Task Search_MatchesOnTheCodeValue()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" },
            new CodeSystemCode { Value = "1160-1", Display = "Pediatric Medical Ward" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "1026", CodeSystem = Hsloc });

        Assert.Equal("1026-4", Assert.Single(result.Records).Code);
    }

    [Fact]
    public async Task Search_MatchesOnTheDisplay()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" },
            new CodeSystemCode { Value = "1160-1", Display = "Pediatric Medical Ward" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "burn", CodeSystem = Hsloc });

        Assert.Equal("1026-4", Assert.Single(result.Records).Code);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "BURN CRITICAL", CodeSystem = Hsloc });

        Assert.Single(result.Records);
    }

    [Fact]
    public async Task Search_WithoutSearchText_ReturnsEverythingInScope()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" },
            new CodeSystemCode { Value = "1160-1", Display = "Pediatric Medical Ward" }));

        var result = await _service.Search(new CodeSearchQuery { CodeSystem = Hsloc });

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(2, result.Metadata.TotalCount);
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsEmptyRecordsAndZeroTotal()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "nothing", CodeSystem = Hsloc });

        Assert.Empty(result.Records);
        Assert.Equal(0, result.Metadata.TotalCount);
    }

    // ----------------------------------------------------------------- scoping

    [Fact]
    public async Task Search_CodeSystemScope_SearchesOnlyThatCodeSystem()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "care", CodeSystem = Hsloc });

        Assert.Equal(Hsloc, Assert.Single(result.Records).System);
        _cache.Verify(x => x.GetAllCodeGroups(It.IsAny<CodeGroup.CodeGroupTypes>()), Times.Never);
    }

    [Fact]
    public async Task Search_ValueSetScope_SearchesOnlyThatValueSet()
    {
        GivenValueSet(ValueSetGroup(EncounterValueSet, ActCode,
            new ValueSetCode { Value = "AMB", Display = "ambulatory" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "amb", ValueSet = EncounterValueSet });

        var record = Assert.Single(result.Records);
        Assert.Equal("AMB", record.Code);

        // The system reported for a value set member is the code system it belongs to, not the value set.
        Assert.Equal(ActCode, record.System);
        _cache.Verify(x => x.GetAllCodeGroups(It.IsAny<CodeGroup.CodeGroupTypes>()), Times.Never);
    }

    [Fact]
    public async Task Search_NoScope_SearchesEveryCodeSystemAndValueSet()
    {
        GivenAllContent(
            [CodeSystemGroup(Hsloc, "1.0.0", new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" })],
            [ValueSetGroup(EncounterValueSet, ActCode, new ValueSetCode { Value = "CARE", Display = "Care plan" })]);

        var result = await _service.Search(new CodeSearchQuery { Search = "care" });

        Assert.Equal(2, result.Metadata.TotalCount);
        Assert.Contains(result.Records, r => r.System == Hsloc);
        Assert.Contains(result.Records, r => r.System == ActCode);
    }

    // -------------------------------------------------------- unloaded content

    [Fact]
    public async Task Search_UnloadedCodeSystem_IsRejectedNamingTheParameter()
    {
        _cache.Setup(x => x.GetCodeGroup(
                It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((CodeGroup?)null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.Search(new CodeSearchQuery { CodeSystem = "http://nope" }));

        Assert.Equal(CodeSearchParameters.CodeSystem, ex.ParamName);
    }

    [Fact]
    public async Task Search_UnloadedValueSet_IsRejectedNamingTheParameter()
    {
        _cache.Setup(x => x.GetCodeGroup(
                It.IsAny<CodeGroup.CodeGroupTypes>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((CodeGroup?)null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.Search(new CodeSearchQuery { ValueSet = "http://nope" }));

        Assert.Equal(CodeSearchParameters.ValueSet, ex.ParamName);
    }

    /// <summary>
    /// The cache answers an unloaded version with the latest one, so the failure this pins is not "no
    /// result" but "the wrong result served silently".
    /// </summary>
    [Fact]
    public async Task Search_UnloadedVersion_IsRejectedRatherThanAnsweredWithTheLatest()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" }));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.Search(new CodeSearchQuery { CodeSystem = Hsloc, Version = "9.9.9" }));

        Assert.Equal(CodeSearchParameters.Version, ex.ParamName);
    }

    [Fact]
    public async Task Search_LoadedVersion_IsHonoured()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" }));

        var result = await _service.Search(new CodeSearchQuery { CodeSystem = Hsloc, Version = "1.0.0" });

        Assert.Single(result.Records);
    }

    /// <summary>
    /// A canonical may carry its version as a "|" suffix. The version actually requested has to be the one
    /// verified, or a piped version is never checked at all.
    /// </summary>
    [Fact]
    public async Task Search_PipedCanonicalCarryingAnUnloadedVersion_IsRejected()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" }));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.Search(new CodeSearchQuery { CodeSystem = $"{Hsloc}|9.9.9" }));

        Assert.Equal(CodeSearchParameters.Version, ex.ParamName);
    }

    // ------------------------------------------------------------ ordering

    [Fact]
    public async Task Search_OrdersExactCodeMatchFirst()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "AMB-EXT", Display = "Ambulatory extended" },
            new CodeSystemCode { Value = "AMB", Display = "Ambulatory" },
            new CodeSystemCode { Value = "AAMB", Display = "Ambulatory, other" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "AMB", CodeSystem = ActCode });

        Assert.Equal("AMB", result.Records.First().Code);
    }

    [Fact]
    public async Task Search_OrdersByCodeThenSystem()
    {
        GivenAllContent(
            [
                CodeSystemGroup("http://b.example", "1.0.0", new CodeSystemCode { Value = "shared", Display = "B" }),
                CodeSystemGroup("http://a.example", "1.0.0",
                    new CodeSystemCode { Value = "shared", Display = "A" },
                    new CodeSystemCode { Value = "aaa-shared", Display = "A first" })
            ],
            []);

        // "shar" rather than "shared" so no candidate is an exact code match; this is about the
        // code-then-system tie-break alone, which the exact-match rule would otherwise mask.
        var result = await _service.Search(new CodeSearchQuery { Search = "shar" });

        Assert.Collection(result.Records,
            r => Assert.Equal("aaa-shared", r.Code),
            r =>
            {
                Assert.Equal("shared", r.Code);
                Assert.Equal("http://a.example", r.System);
            },
            r =>
            {
                Assert.Equal("shared", r.Code);
                Assert.Equal("http://b.example", r.System);
            });
    }

    // -------------------------------------------------------------- paging

    [Fact]
    public async Task Search_TotalCountIsTheFullMatchCountBeforePaging()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            Enumerable.Range(1, 25)
                .Select(i => new CodeSystemCode { Value = $"code-{i:D2}", Display = "Match" })
                .ToArray<Code>()));

        var result = await _service.Search(
            new CodeSearchQuery { Search = "Match", CodeSystem = ActCode, PageSize = 10 });

        Assert.Equal(10, result.Records.Count);
        Assert.Equal(25, result.Metadata.TotalCount);
        Assert.Equal(3, result.Metadata.TotalPages);
    }

    /// <summary>
    /// The point of a total ordering: no record may appear on two pages, and none may fall between them.
    /// </summary>
    [Fact]
    public async Task Search_PagesDoNotOverlapOrDropRecords()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            Enumerable.Range(1, 25)
                .Select(i => new CodeSystemCode { Value = $"code-{i:D2}", Display = "Match" })
                .ToArray<Code>()));

        var pages = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await _service.Search(
                new CodeSearchQuery { Search = "Match", CodeSystem = ActCode, PageSize = 10, PageNumber = page });
            pages.AddRange(result.Records.Select(r => r.Code));
        }

        Assert.Equal(25, pages.Count);
        Assert.Equal(25, pages.Distinct().Count());
    }

    [Fact]
    public async Task Search_RepeatingTheSameQueryReturnsTheSameRecords()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            Enumerable.Range(1, 25)
                .Select(i => new CodeSystemCode { Value = $"code-{i:D2}", Display = "Match" })
                .ToArray<Code>()));

        var query = new CodeSearchQuery { Search = "Match", CodeSystem = ActCode, PageSize = 10, PageNumber = 2 };

        var first = await _service.Search(query);
        var second = await _service.Search(query);

        Assert.Equal(first.Records.Select(r => r.Code), second.Records.Select(r => r.Code));
    }

    [Fact]
    public async Task Search_ClampsPageSizeToTheServerMaximum()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            Enumerable.Range(1, 150)
                .Select(i => new CodeSystemCode { Value = $"code-{i:D3}", Display = "Match" })
                .ToArray<Code>()));

        var result = await _service.Search(
            new CodeSearchQuery { Search = "Match", CodeSystem = ActCode, PageSize = 5000 });

        Assert.Equal(CodeSearchDefaults.MaxPageSize, result.Records.Count);
        Assert.Equal(CodeSearchDefaults.MaxPageSize, result.Metadata.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task Search_ClampsPageSizeAndNumberBelowOne(int value)
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "code-1", Display = "Match" }));

        var result = await _service.Search(new CodeSearchQuery
        {
            Search = "Match",
            CodeSystem = ActCode,
            PageSize = value,
            PageNumber = value
        });

        Assert.Equal(1, result.Metadata.PageSize);
        Assert.Equal(1, result.Metadata.PageNumber);
        Assert.Single(result.Records);
    }

    [Fact]
    public async Task Search_PageBeyondTheEnd_ReturnsAnEmptyPageRatherThanFailing()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "code-1", Display = "Match" }));

        var result = await _service.Search(
            new CodeSearchQuery { Search = "Match", CodeSystem = ActCode, PageNumber = 99 });

        Assert.Empty(result.Records);
        Assert.Equal(1, result.Metadata.TotalCount);
    }

    // ------------------------------------------------------- de-duplication

    /// <summary>
    /// A CSV may list a code twice with differing status; the later row is the effective one
    /// (LEGLINK-599/814), and the whole record follows it, not just the status.
    /// </summary>
    [Fact]
    public async Task Search_DuplicateWithinAGroup_KeepsTheLastOccurrence()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "DUP", Display = "First", Status = CodeStatus.Active },
            new CodeSystemCode { Value = "DUP", Display = "Second", Status = CodeStatus.Inactive }));

        var result = await _service.Search(new CodeSearchQuery { Search = "DUP", CodeSystem = ActCode });

        var record = Assert.Single(result.Records);
        Assert.Equal("Second", record.Display);
        Assert.Equal(CodeStatus.Inactive, record.Status);
    }

    /// <summary>
    /// Reachable from both a code system and a value set, the code system is the defining source and wins.
    /// </summary>
    [Fact]
    public async Task Search_SameCodeInACodeSystemAndAValueSet_ReturnsOneRecordFromTheCodeSystem()
    {
        GivenAllContent(
            [CodeSystemGroup(ActCode, "1.0.0",
                new CodeSystemCode { Value = "AMB", Display = "From the code system", Status = CodeStatus.Active })],
            [ValueSetGroup(EncounterValueSet, ActCode,
                new ValueSetCode { Value = "AMB", Display = "From the value set", Status = CodeStatus.Inactive })]);

        var result = await _service.Search(new CodeSearchQuery { Search = "AMB" });

        var record = Assert.Single(result.Records);
        Assert.Equal("From the code system", record.Display);
        Assert.Equal(CodeStatus.Active, record.Status);
    }

    // -------------------------------------------------------------- status

    [Fact]
    public async Task Search_ReturnsInactiveCodesByDefault_MarkedAsSuch()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "OLD", Display = "Retired", Status = CodeStatus.Inactive }));

        var result = await _service.Search(new CodeSearchQuery { Search = "OLD", CodeSystem = ActCode });

        Assert.Equal(CodeStatus.Inactive, Assert.Single(result.Records).Status);
    }

    [Fact]
    public async Task Search_ExcludeInactive_OmitsThemAndTheyDoNotCountTowardTheTotal()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "OLD", Display = "Retired thing", Status = CodeStatus.Inactive },
            new CodeSystemCode { Value = "NEW", Display = "Current thing", Status = CodeStatus.Active }));

        var result = await _service.Search(
            new CodeSearchQuery { Search = "thing", CodeSystem = ActCode, ExcludeInactive = true });

        Assert.Equal("NEW", Assert.Single(result.Records).Code);
        Assert.Equal(1, result.Metadata.TotalCount);
    }

    /// <summary>
    /// A value set member's own membership status overrides the code system's, which is the rule
    /// <c>$validate-code</c> applies and the subtle half of LEGLINK-889.
    /// </summary>
    [Fact]
    public async Task Search_ValueSetMembershipStatus_OverridesTheCodeSystem()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "AMB", Display = "Ambulatory", Status = CodeStatus.Active }));
        GivenValueSet(ValueSetGroup(EncounterValueSet, ActCode,
            new ValueSetCode { Value = "AMB", Display = "Ambulatory", Status = CodeStatus.Inactive }));

        var result = await _service.Search(new CodeSearchQuery { Search = "AMB", ValueSet = EncounterValueSet });

        Assert.Equal(CodeStatus.Inactive, Assert.Single(result.Records).Status);
    }

    /// <summary>
    /// A member loaded without a status column carries none of its own, so it is rejoined to the code
    /// system named by its system key.
    /// </summary>
    [Fact]
    public async Task Search_ValueSetMemberWithoutItsOwnStatus_TakesTheCodeSystemStatus()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "AMB", Display = "Ambulatory", Status = CodeStatus.Inactive }));
        GivenValueSet(ValueSetGroup(EncounterValueSet, ActCode,
            new Code { Value = "AMB", Display = "Ambulatory" }));

        var result = await _service.Search(new CodeSearchQuery { Search = "AMB", ValueSet = EncounterValueSet });

        Assert.Equal(CodeStatus.Inactive, Assert.Single(result.Records).Status);
    }

    /// <summary>
    /// The order must not shift with the server's culture. Ordinal puts "B-code" before "a-code" because
    /// 'B' is 66 and 'a' is 97; a culture comparer puts "a-code" first. Swapping StringComparer.Ordinal
    /// for the culture one would reorder these two and nothing else in this suite would notice.
    /// </summary>
    [Fact]
    public async Task Search_OrdersOrdinallyRatherThanByCulture()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "a-code", Display = "Lower first" },
            new CodeSystemCode { Value = "B-code", Display = "Upper second" }));

        // "code" matches both and is equal to neither, so the exact-match rule does not decide this.
        var result = await _service.Search(new CodeSearchQuery { Search = "code", CodeSystem = ActCode });

        Assert.Collection(result.Records,
            r => Assert.Equal("B-code", r.Code),
            r => Assert.Equal("a-code", r.Code));
    }

    /// <summary>
    /// The rejection case is covered above; without this one the canonical splitting could be broken to
    /// refuse every piped version and the suite would stay green.
    /// </summary>
    [Fact]
    public async Task Search_PipedCanonicalCarryingALoadedVersion_IsAccepted()
    {
        GivenCodeSystem(CodeSystemGroup(Hsloc, "1.0.0",
            new CodeSystemCode { Value = "1026-4", Display = "Burn Critical Care" }));

        var result = await _service.Search(new CodeSearchQuery { CodeSystem = $"{Hsloc}|1.0.0" });

        Assert.Single(result.Records);
    }

    /// <summary>
    /// Reachable from two value sets and no code system, the code still collapses to one record. Which of
    /// the two supplies the display is deliberately not asserted: the rule is "the last group walked
    /// wins", which is arbitrary, and the precedence question is still open on LEGLINK-1007. What has to
    /// hold is that the answer is single and repeatable.
    /// </summary>
    [Fact]
    public async Task Search_SameCodeInTwoValueSets_CollapsesToOneStableRecord()
    {
        GivenAllContent(
            [],
            [
                ValueSetGroup("http://example.org/vs-one", ActCode,
                    new ValueSetCode { Value = "AMB", Display = "From the first value set" }),
                ValueSetGroup("http://example.org/vs-two", ActCode,
                    new ValueSetCode { Value = "AMB", Display = "From the second value set" })
            ]);

        var first = await _service.Search(new CodeSearchQuery { Search = "AMB" });
        var second = await _service.Search(new CodeSearchQuery { Search = "AMB" });

        Assert.Single(first.Records);
        Assert.Equal(1, first.Metadata.TotalCount);
        Assert.Equal(first.Records[0].Display, second.Records[0].Display);
    }

    // -------------------------------------------------------- cancellation

    [Fact]
    public async Task Search_HonoursCancellation()
    {
        GivenCodeSystem(CodeSystemGroup(ActCode, "1.0.0",
            new CodeSystemCode { Value = "AMB", Display = "Ambulatory" }));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.Search(new CodeSearchQuery { Search = "AMB", CodeSystem = ActCode }, cts.Token));
    }
}
