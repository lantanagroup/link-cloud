using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Xunit;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Covers the support surface's own mapper. It deliberately shares nothing with
/// <see cref="EntryMapperTests"/>: the two surfaces have separate models so that replacing
/// the third party's contract cannot disturb our seeding endpoints.
/// </summary>
public class MockEntryMapperTests
{
    private static ReportingPlanEntryEntity Entry(
        string measure = "HOB",
        string isReporting = "Y",
        int? month = 5,
        string component = ReportingComponents.Msc) => new()
        {
            Id = "11111111-1111-1111-1111-111111111111",
            FacilityId = "F1",
            Component = component,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = 2026,
            IsReporting = isReporting,
            CreateDate = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            ModifyDate = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc)
        };

    [Fact]
    public void ToModel_CarriesEveryField()
    {
        var model = MockEntryMapper.ToModel(Entry());

        model.Id.Should().Be("11111111-1111-1111-1111-111111111111");
        model.FacilityId.Should().Be("F1");
        model.Component.Should().Be("MSC");
        model.Measure.Should().Be("HOB");
        model.ReportingMonth.Should().Be(5);
        model.ReportingYear.Should().Be(2026);
        model.IsReporting.Should().Be("Y");
        model.CreateDate.Should().Be(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        model.ModifyDate.Should().Be(new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ToModel_ForAnAnnualEntry_KeepsTheMonthNull()
    {
        // Defaulting a missing month to zero would make a patient-safety entry look like a
        // monthly one, and round-tripping it back through the create endpoint would then be
        // rejected for carrying a month it never had.
        var model = MockEntryMapper.ToModel(Entry(month: null, component: ReportingComponents.Ps));

        model.ReportingMonth.Should().BeNull();
    }

    [Fact]
    public void ToModel_TreatsStoredTimestampsAsUtc()
    {
        // The column is datetime2 with no offset, so EF hands back Unspecified. Left
        // unqualified, the conversion would silently apply the host's local offset and a
        // developer in one timezone would see different timestamps than the CI agent.
        var entry = Entry();
        entry.CreateDate = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Unspecified);

        var model = MockEntryMapper.ToModel(entry);

        model.CreateDate.Offset.Should().Be(TimeSpan.Zero);
        model.CreateDate.UtcDateTime.Hour.Should().Be(12);
    }

    [Fact]
    public void ToModel_WithNoModifyDate_LeavesItNull()
    {
        var entry = Entry();
        entry.ModifyDate = null;

        MockEntryMapper.ToModel(entry).ModifyDate.Should().BeNull();
    }

    [Fact]
    public void ToEntity_FromCreateRequest_LeavesIdentityAndTimestampsToTheStore()
    {
        var request = new MockEntryRequest
        {
            FacilityId = "F1",
            Component = "MSC",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y"
        };

        var entity = MockEntryMapper.ToEntity(request);

        entity.FacilityId.Should().Be("F1");
        entity.Component.Should().Be("MSC");
        entity.Measure.Should().Be("HOB");
        entity.CreateDate.Should().Be(default);
        entity.ModifyDate.Should().BeNull();
    }

    [Fact]
    public void ToEntity_ForAnAnnualRequest_CarriesTheAbsentMonthThrough()
    {
        var request = new MockEntryRequest
        {
            FacilityId = "F1",
            Component = "PS",
            Measure = "HAI",
            ReportingYear = 2026
        };

        MockEntryMapper.ToEntity(request).ReportingMonth.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToEntity_WithoutIsReporting_DefaultsToY(string? isReporting)
    {
        var request = new MockEntryRequest
        {
            FacilityId = "F1",
            Component = "MSC",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = isReporting
        };

        MockEntryMapper.ToEntity(request).IsReporting.Should().Be("Y");
    }

    [Fact]
    public void ToEntity_FromUpdateRequest_CarriesTheIdentifier()
    {
        var request = new MockEntryUpdateRequest
        {
            Id = "abc",
            FacilityId = "F1",
            Component = "MSC",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y"
        };

        MockEntryMapper.ToEntity(request).Id.Should().Be("abc");
    }

    [Fact]
    public void ToPage_CarriesRecordsAndPagingMetadata()
    {
        var page = MockEntryMapper.ToPage([Entry(), Entry(measure: "HTCDI")], new PaginationMetadata(10, 2, 25));

        page.Records.Should().HaveCount(2);
        page.Metadata.PageSize.Should().Be(10);
        page.Metadata.PageNumber.Should().Be(2);
        page.Metadata.TotalCount.Should().Be(25);
        page.Metadata.TotalPages.Should().Be(3);
    }

    [Fact]
    public void Mappers_RejectNullInput()
    {
        ((Action)(() => MockEntryMapper.ToModel(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => MockEntryMapper.ToEntity((MockEntryRequest)null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => MockEntryMapper.ToPage(null!, new PaginationMetadata(10, 1, 0)))).Should().Throw<ArgumentNullException>();
        ((Action)(() => MockEntryMapper.ToPage([], null!))).Should().Throw<ArgumentNullException>();
    }
}
