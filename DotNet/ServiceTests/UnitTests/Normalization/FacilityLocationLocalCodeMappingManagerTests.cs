using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.FacilityLocations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class FacilityLocationLocalCodeMappingManagerTests : IDisposable
{
    private const string FacilityId = "facility-1";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly NormalizationDbContext _context;
    private readonly Mock<IHSLOCQueries> _hslocQueries = new(MockBehavior.Strict);

    public FacilityLocationLocalCodeMappingManagerTests()
    {
        _connection.Open();
        _context = new NormalizationDbContext(new DbContextOptionsBuilder<NormalizationDbContext>()
            .UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateFacilityLocationLocalCodeMappings_NullOrEmptyInput_DoesNothing(bool useNull)
    {
        var locations = new Mock<IFacilityLocationManager>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await CreateManager(locations.Object).UpdateFacilityLocationLocalCodeMappings(
            FacilityId, useNull ? null! : [], cancellation.Token);

        Assert.Empty(await _context.FacilityLocations.ToListAsync());
        Assert.Empty(await _context.FacilityLocationLocalCodeMappings.ToListAsync());
        locations.VerifyNoOtherCalls();
        _hslocQueries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateFacilityLocationLocalCodeMappings_NewLocationWithoutCodes_CreatesLocationWithoutLoadingHSLOC()
    {
        var result = CreateResult();

        await CreateManager().UpdateFacilityLocationLocalCodeMappings(FacilityId, [result]);

        var stored = Assert.Single(await _context.FacilityLocations.AsNoTracking().ToListAsync());
        Assert.Equal(FacilityId, stored.FacilityId);
        Assert.Equal(result.Location.Id, stored.LocationId);
        Assert.Equal("Ward", stored.LocationName);
        Assert.Equal("Alias one, Alias two", stored.LocationAlias);
        Assert.Equal("parent", stored.PartOfId);
        Assert.Empty(await _context.FacilityLocationLocalCodeMappings.ToListAsync());
        _hslocQueries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateFacilityLocationLocalCodeMappings_NewCodes_PersistsMappingsAndLoadsHSLOCOnce()
    {
        var hsloc = await SeedHSLOC();
        using var cancellation = new CancellationTokenSource();
        _hslocQueries.Setup(queries => queries.GetActiveLookup(cancellation.Token)).ReturnsAsync(new Dictionary<string, Guid> { [hsloc.HSLOCCode] = hsloc.Id });
        var result = CreateResult(new HSLOCMappingResultCode
        {
            SourceSystem = "local-system", SourceCode = "ward", TargetCode = hsloc.HSLOCCode
        }, new HSLOCMappingResultCode
        {
            SourceSystem = null, SourceCode = null, TargetCode = "unknown"
        });

        await CreateManager().UpdateFacilityLocationLocalCodeMappings(FacilityId, [result], cancellation.Token);

        var location = Assert.Single(await _context.FacilityLocations.AsNoTracking().ToListAsync());
        var mappings = await _context.FacilityLocationLocalCodeMappings.AsNoTracking().ToListAsync();
        Assert.Equal(2, mappings.Count);
        Assert.All(mappings, mapping => Assert.Equal(location.Id, mapping.FacilityLocationId));
        var mapped = Assert.Single(mappings, mapping => mapping.LocalCode == "ward");
        Assert.Equal("local-system", mapped.LocalCodeSystem);
        Assert.Equal(hsloc.Id, mapped.HSLOCId);
        var unmapped = Assert.Single(mappings, mapping => mapping.LocalCode == "");
        Assert.Equal("", unmapped.LocalCodeSystem);
        Assert.Null(unmapped.HSLOCId);
        _hslocQueries.Verify(queries => queries.GetActiveLookup(cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("local-system", null)]
    [InlineData(null, "ward")]
    public async Task UpdateFacilityLocationLocalCodeMappings_NullSourceValues_UpdatesExistingMapping(string? sourceSystem, string? sourceCode)
    {
        var hsloc = await SeedHSLOC();
        _hslocQueries.Setup(queries => queries.GetActiveLookup(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, Guid> { [hsloc.HSLOCCode] = hsloc.Id });
        var code = new HSLOCMappingResultCode
        {
            SourceSystem = sourceSystem, SourceCode = sourceCode, TargetCode = "unknown"
        };
        var result = CreateResult(code);
        var manager = CreateManager();

        await manager.UpdateFacilityLocationLocalCodeMappings(FacilityId, [result]);
        var original = Assert.Single(await _context.FacilityLocationLocalCodeMappings.AsNoTracking().ToListAsync());
        Assert.Null(original.HSLOCId);
        _context.ChangeTracker.Clear();
        code.TargetCode = hsloc.HSLOCCode;

        await manager.UpdateFacilityLocationLocalCodeMappings(FacilityId, [result]);

        var stored = Assert.Single(await _context.FacilityLocationLocalCodeMappings.AsNoTracking().ToListAsync());
        Assert.Equal(original.Id, stored.Id);
        Assert.Equal(sourceSystem ?? string.Empty, stored.LocalCodeSystem);
        Assert.Equal(sourceCode ?? string.Empty, stored.LocalCode);
        Assert.Equal(hsloc.Id, stored.HSLOCId);
        Assert.NotNull(stored.ModifyDate);

        _context.ChangeTracker.Clear();
        await manager.UpdateFacilityLocationLocalCodeMappings(FacilityId, [result]);
        Assert.Equal(stored.ModifyDate, (await _context.FacilityLocationLocalCodeMappings.AsNoTracking().SingleAsync()).ModifyDate);
    }

    [Theory]
    [InlineData(MappingTargetSystems.HslocUrl, true)]
    [InlineData(MappingTargetSystems.HslocOid, true)]
    [InlineData(MappingTargetSystems.HslocUrl, false)]
    [InlineData(MappingTargetSystems.HslocOid, false)]
    public async Task UpdateFacilityLocationLocalCodeMappings_HSLOCSource_UsesSourceInsteadOfTarget(string sourceSystem, bool knownSource)
    {
        var hsloc = await SeedHSLOC();
        _hslocQueries.Setup(queries => queries.GetActiveLookup(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, Guid> { [hsloc.HSLOCCode] = hsloc.Id });
        var result = CreateResult(new HSLOCMappingResultCode
        {
            SourceSystem = sourceSystem,
            SourceCode = knownSource ? hsloc.HSLOCCode : "unknown",
            TargetCode = knownSource ? "unknown" : hsloc.HSLOCCode
        });

        await CreateManager().UpdateFacilityLocationLocalCodeMappings(FacilityId, [result]);

        var stored = Assert.Single(await _context.FacilityLocationLocalCodeMappings.AsNoTracking().ToListAsync());
        Assert.Equal(knownSource ? hsloc.Id : (Guid?)null, stored.HSLOCId);
        Assert.Equal(sourceSystem, stored.LocalCodeSystem);
        Assert.Equal(result.LocationTypeCodes[0].SourceCode, stored.LocalCode);
    }

    [Fact]
    public async Task UpdateFacilityLocationLocalCodeMappings_ChangedLocation_UpdatesOnlyRequestedFacility()
    {
        var location = new FacilityLocation { FacilityId = FacilityId, LocationId = "location-1", LocationName = "Old" };
        var other = new FacilityLocation { FacilityId = "other-facility", LocationId = "location-1", LocationName = "Other" };
        _context.FacilityLocations.AddRange(location, other);
        await _context.SaveChangesAsync();

        await CreateManager().UpdateFacilityLocationLocalCodeMappings(FacilityId, [CreateResult()]);

        var stored = await _context.FacilityLocations.AsNoTracking().SingleAsync(row => row.Id == location.Id);
        Assert.Equal("Ward", stored.LocationName);
        Assert.Equal("Alias one, Alias two", stored.LocationAlias);
        Assert.Equal("parent", stored.PartOfId);
        Assert.NotNull(stored.ModifyDate);
        var untouched = await _context.FacilityLocations.AsNoTracking().SingleAsync(row => row.Id == other.Id);
        Assert.Equal("Other", untouched.LocationName);
        Assert.Null(untouched.ModifyDate);
        _hslocQueries.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateFacilityLocationLocalCodeMappings_ChangedMapping_UpdatesOrClearsHSLOC(bool clearMapping)
    {
        var hsloc = await SeedHSLOC();
        var mapping = await SeedMapping(clearMapping ? hsloc.Id : null);
        _hslocQueries.Setup(queries => queries.GetActiveLookup(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, Guid> { [hsloc.HSLOCCode] = hsloc.Id });
        var result = CreateResult(new HSLOCMappingResultCode
        {
            SourceSystem = "local-system", SourceCode = "ward", TargetCode = clearMapping ? "unknown" : hsloc.HSLOCCode
        });

        await CreateManager().UpdateFacilityLocationLocalCodeMappings(FacilityId, [result]);

        var stored = Assert.Single(await _context.FacilityLocationLocalCodeMappings.AsNoTracking().ToListAsync());
        Assert.Equal(mapping.Id, stored.Id);
        Assert.Equal(clearMapping ? (Guid?)null : hsloc.Id, stored.HSLOCId);
        Assert.Equal("local-system", stored.LocalCodeSystem);
        Assert.Equal("ward", stored.LocalCode);
        Assert.NotNull(stored.ModifyDate);
    }

    [Fact]
    public async Task UpdateFacilityLocationLocalCodeMappings_UnchangedRecords_DoesNotUpdate()
    {
        var hsloc = await SeedHSLOC();
        await SeedMapping(hsloc.Id);
        _hslocQueries.Setup(queries => queries.GetActiveLookup(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, Guid> { [hsloc.HSLOCCode] = hsloc.Id });
        var locations = new Mock<IFacilityLocationManager>(MockBehavior.Strict);
        var result = CreateResult(new HSLOCMappingResultCode
        {
            SourceSystem = "local-system", SourceCode = "ward", TargetCode = hsloc.HSLOCCode
        });

        await CreateManager(locations.Object).UpdateFacilityLocationLocalCodeMappings(FacilityId, [result]);

        Assert.Null((await _context.FacilityLocations.AsNoTracking().SingleAsync()).ModifyDate);
        Assert.Null((await _context.FacilityLocationLocalCodeMappings.AsNoTracking().SingleAsync()).ModifyDate);
        locations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateFacilityLocationLocalCodeMappings_RepeatedLocationAndCode_DoesNotDuplicateAndContinues()
    {
        _hslocQueries.Setup(queries => queries.GetActiveLookup(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, Guid>());
        var code = new HSLOCMappingResultCode { SourceSystem = "local-system", SourceCode = "ward" };
        var first = CreateResult(code, code);
        var second = CreateResult(code, new HSLOCMappingResultCode { SourceSystem = "local-system", SourceCode = "second" });

        await CreateManager().UpdateFacilityLocationLocalCodeMappings(FacilityId, [first, second]);

        Assert.Single(await _context.FacilityLocations.ToListAsync());
        var mappings = await _context.FacilityLocationLocalCodeMappings.AsNoTracking().ToListAsync();
        Assert.Equal(2, mappings.Count);
        Assert.Single(mappings, mapping => mapping.LocalCode == "ward");
        Assert.Single(mappings, mapping => mapping.LocalCode == "second");
        _hslocQueries.Verify(queries => queries.GetActiveLookup(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateFacilityLocationLocalCodeMappings_UnrelatedCreateFailure_Propagates()
    {
        var locations = new Mock<IFacilityLocationManager>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("Unexpected failure");
        locations.Setup(manager => manager.Create(FacilityId, It.IsAny<FacilityLocationPostModel>(), cancellation.Token))
            .ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateManager(locations.Object)
            .UpdateFacilityLocationLocalCodeMappings(FacilityId, [CreateResult()], cancellation.Token));

        Assert.Same(failure, actual);
        _hslocQueries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateFacilityLocationLocalCodeMappings_CanceledInput_ThrowsWithoutCreatingLocation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var locations = new Mock<IFacilityLocationManager>(MockBehavior.Strict);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateManager(locations.Object)
            .UpdateFacilityLocationLocalCodeMappings(FacilityId, [CreateResult()], cancellation.Token));

        Assert.Empty(await _context.FacilityLocations.ToListAsync());
        locations.VerifyNoOtherCalls();
        _hslocQueries.VerifyNoOtherCalls();
    }

    private FacilityLocationLocalCodeMappingManager CreateManager(IFacilityLocationManager? locations = null) => new(
        _context, locations ?? new FacilityLocationManager(_context),
        new FacilityLocationLocalCodeMappingQueries(_context), _hslocQueries.Object,
        NullLogger<FacilityLocationLocalCodeMappingManager>.Instance);

    private static HSLOCMappingResult CreateResult(params HSLOCMappingResultCode[] codes) => new(FacilityId, new Location
    {
        Id = "location-1", Name = "Ward", Alias = ["Alias one", "Alias two"],
        PartOf = new ResourceReference("Location/parent")
    }) { LocationTypeCodes = codes.ToList() };

    private async Task<HSLOC> SeedHSLOC()
    {
        var hsloc = new HSLOC
        {
            CDCCode = "cdc", ShortDescription = "short", HSLOCCode = "A1",
            LongDescription = "long", Version = "2026"
        };
        _context.HSLOCS.Add(hsloc);
        await _context.SaveChangesAsync();
        return hsloc;
    }

    private async Task<FacilityLocationLocalCodeMapping> SeedMapping(Guid? hslocId)
    {
        var location = new FacilityLocation
        {
            FacilityId = FacilityId, LocationId = "location-1", LocationName = "Ward",
            LocationAlias = "Alias one, Alias two", PartOfId = "parent"
        };
        var mapping = new FacilityLocationLocalCodeMapping
        {
            FacilityLocation = location, FacilityLocationId = location.Id,
            LocalCodeSystem = "local-system", LocalCode = "ward", HSLOCId = hslocId
        };
        _context.FacilityLocationLocalCodeMappings.Add(mapping);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return mapping;
    }
}