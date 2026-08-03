using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Mapping;
using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Xunit;

namespace UnitTests.MockDmrpApi;

public class EntryMapperTests
{
    private static ReportingPlanEntryEntity Entry(string measure = "HOB", string isReporting = "Y") => new()
    {
        Id = "11111111-1111-1111-1111-111111111111",
        FacilityId = "F1",
        Measure = measure,
        ReportingMonth = 5,
        ReportingYear = 2026,
        IsReporting = isReporting,
        CreateDate = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
        ModifyDate = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void ToContract_CarriesEveryField()
    {
        var contract = EntryMapper.ToContract(Entry());

        contract.Id.Should().Be("11111111-1111-1111-1111-111111111111");
        contract.FacilityId.Should().Be("F1");
        contract.Measure.Should().Be("HOB");
        contract.ReportingMonth.Should().Be(5);
        contract.ReportingYear.Should().Be(2026);
        contract.IsReporting.Should().Be("Y");
        contract.CreateDate.Should().Be(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        contract.ModifyDate.Should().Be(new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ToContract_TreatsStoredTimestampsAsUtc()
    {
        // The column is datetime2 with no offset, so EF hands back Unspecified. Left
        // unqualified, the conversion would silently apply the host's local offset and a
        // developer in one timezone would see different timestamps than the CI agent.
        var entry = Entry();
        entry.CreateDate = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Unspecified);

        var contract = EntryMapper.ToContract(entry);

        contract.CreateDate.Offset.Should().Be(TimeSpan.Zero);
        contract.CreateDate.UtcDateTime.Hour.Should().Be(12);
    }

    [Fact]
    public void ToContract_WithNoModifyDate_LeavesItNull()
    {
        var entry = Entry();
        entry.ModifyDate = null;

        EntryMapper.ToContract(entry).ModifyDate.Should().BeNull();
    }

    [Fact]
    public void ToEntity_FromCreateRequest_LeavesIdentityAndTimestampsToTheStore()
    {
        var request = new ReportingPlanEntryRequest
        {
            FacilityId = "F1",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y"
        };

        var entity = EntryMapper.ToEntity(request);

        entity.FacilityId.Should().Be("F1");
        entity.Measure.Should().Be("HOB");
        entity.CreateDate.Should().Be(default);
        entity.ModifyDate.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToEntity_WithoutIsReporting_DefaultsToY(string? isReporting)
    {
        var request = new ReportingPlanEntryRequest
        {
            FacilityId = "F1",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = isReporting!
        };

        EntryMapper.ToEntity(request).IsReporting.Should().Be("Y");
    }

    [Fact]
    public void ToEntity_FromUpdateRequest_CarriesTheIdentifier()
    {
        var request = new ReportingPlanEntry
        {
            Id = "abc",
            FacilityId = "F1",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y"
        };

        EntryMapper.ToEntity(request).Id.Should().Be("abc");
    }

    [Fact]
    public void ToPage_CarriesRecordsAndPagingMetadata()
    {
        var page = EntryMapper.ToPage([Entry(), Entry(measure: "HTCDI")], new PaginationMetadata(10, 2, 25));

        page.Records.Should().HaveCount(2);
        page.Metadata.PageSize.Should().Be(10);
        page.Metadata.PageNumber.Should().Be(2);
        page.Metadata.TotalCount.Should().Be(25);
        page.Metadata.TotalPages.Should().Be(3);
    }

    [Fact]
    public void ToReportingPlan_ListsOnlyTheSuppliedMeasures()
    {
        var retrievedOn = DateTimeOffset.UtcNow;

        var plan = EntryMapper.ToReportingPlan("F1", 5, 2026, [Entry(measure: "HOB")], retrievedOn);

        plan.FacilityId.Should().Be("F1");
        plan.ReportingMonth.Should().Be(5);
        plan.ReportingYear.Should().Be(2026);
        plan.RetrievedOn.Should().Be(retrievedOn);
        plan.Measures.Should().ContainSingle();
        plan.Measures.Single().Measure.Should().Be("HOB");

        // The absence of HTCDI is what tells a caller the facility is not enrolled in it.
        plan.Measures.Should().NotContain(m => m.Measure == "HTCDI");
    }

    [Fact]
    public void ToReportingPlan_WithNoEntries_ProducesAnEmptyMeasuresArrayNotNull()
    {
        // A facility enrolled in nothing is a meaningful answer, and the caller iterates
        // measures unconditionally. A null here would be a NullReferenceException in
        // consumer code for the most ordinary case there is.
        var plan = EntryMapper.ToReportingPlan("F1", 5, 2026, [], DateTimeOffset.UtcNow);

        plan.Measures.Should().NotBeNull();
        plan.Measures.Should().BeEmpty();
    }

    [Fact]
    public void Mappers_RejectNullInput()
    {
        ((Action)(() => EntryMapper.ToContract(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => EntryMapper.ToEntity((ReportingPlanEntryRequest)null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => EntryMapper.ToPage(null!, new PaginationMetadata(10, 1, 0)))).Should().Throw<ArgumentNullException>();
        ((Action)(() => EntryMapper.ToReportingPlan("F1", 5, 2026, null!, DateTimeOffset.UtcNow))).Should().Throw<ArgumentNullException>();
    }
}
