using FluentAssertions;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Automation.Helpers;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Moq;

namespace UnitTests.AutomationLink;

[Trait("Category", "UnitTests")]
public class HslocMappingRunValidatorTests
{
    [Fact]
    public async Task Disabled_WithNoMappings_Passes()
    {
        var client = new Mock<INormalizationServiceClient>();
        client.Setup(c => c.SearchFacilityLocationLocalCodeMappingsAsync(
                It.IsAny<SearchFacilityLocationLocalCodeMappingsRequestApiModel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page());
        var output = new CapturingOutput();
        var sut = new HslocMappingRunValidator(output, client.Object);

        await sut.ValidateAllAsync("facility-1", hslocMapEnabled: false);

        output.Lines.Should().Contain(l => l.Contains("HSLOC MAPPING RUN VALIDATION: Passed", StringComparison.Ordinal));
        client.Verify(c => c.GetHslocCodesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Enabled_EmptyCodeSet_FailsClosed()
    {
        var client = new Mock<INormalizationServiceClient>();
        client.Setup(c => c.SearchFacilityLocationLocalCodeMappingsAsync(
                It.IsAny<SearchFacilityLocationLocalCodeMappingsRequestApiModel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page());
        client.Setup(c => c.GetHslocCodesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new List<HslocCodeApiModel>()));
        var sut = new HslocMappingRunValidator(new CapturingOutput(), client.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync("facility-1", hslocMapEnabled: true));

        ex.Message.Should().Contain("no active codes");
    }

    [Fact]
    public async Task Enabled_GeneratedPatients_RequiresIdentifierMappingAndLocationRow()
    {
        var client = new Mock<INormalizationServiceClient>();
        client.Setup(c => c.GetHslocCodesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new List<HslocCodeApiModel>
            {
                new() { HSLOCCode = "1025-6", IsActive = true }
            }));
        client.Setup(c => c.SearchFacilityLocationLocalCodeMappingsAsync(
                It.IsAny<SearchFacilityLocationLocalCodeMappingsRequestApiModel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(
                new FacilityLocationLocalCodeMappingApiModel
                {
                    FacilityId = "facility-1",
                    LocationId = "abcd1234-Loc-ICU",
                    LocalCodeSystem = HslocMappingDefaults.IdentifierSystem,
                    LocalCode = "abcd1234-Loc-ICU",
                    HSLOCId = Guid.NewGuid(),
                    HSLOCCode = "1025-6"
                },
                new FacilityLocationLocalCodeMappingApiModel
                {
                    FacilityId = "facility-1",
                    LocationId = "abcd1234-Loc-ICU",
                    LocalCodeSystem = "https://nhsnlink.org/location-alias",
                    LocalCode = "Alias, Intensive Care Unit"
                }));
        client.Setup(c => c.GetFacilityLocationAsync("facility-1", "abcd1234-Loc-ICU", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new FacilityLocationApiModel
            {
                FacilityId = "facility-1",
                LocationId = "abcd1234-Loc-ICU",
                LocationName = "Intensive Care Unit",
                LocationAlias = "Alias, Intensive Care Unit",
                PartOfId = "abcd1234-Loc-Hospital"
            }));
        var output = new CapturingOutput();
        var sut = new HslocMappingRunValidator(output, client.Object);

        await sut.ValidateAllAsync("facility-1", hslocMapEnabled: true, ["Patient-abcd1234-001"]);

        output.Lines.Should().Contain(l => l.Contains("HSLOC MAPPING RUN VALIDATION: Passed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Enabled_GeneratedPatients_PreStampedHslocOnly_Fails()
    {
        var client = new Mock<INormalizationServiceClient>();
        client.Setup(c => c.GetHslocCodesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new List<HslocCodeApiModel>
            {
                new() { HSLOCCode = "1025-6", IsActive = true }
            }));
        client.Setup(c => c.SearchFacilityLocationLocalCodeMappingsAsync(
                It.IsAny<SearchFacilityLocationLocalCodeMappingsRequestApiModel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(new FacilityLocationLocalCodeMappingApiModel
            {
                FacilityId = "facility-1",
                LocationId = "abcd1234-Loc-ICU",
                LocalCodeSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html",
                LocalCode = "1025-6",
                HSLOCId = Guid.NewGuid(),
                HSLOCCode = "1025-6"
            }));
        client.Setup(c => c.GetFacilityLocationAsync("facility-1", "abcd1234-Loc-ICU", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(new FacilityLocationApiModel
            {
                FacilityId = "facility-1",
                LocationId = "abcd1234-Loc-ICU",
                LocationName = "Intensive Care Unit",
                LocationAlias = "Alias, Intensive Care Unit",
                PartOfId = "abcd1234-Loc-Hospital"
            }));
        var sut = new HslocMappingRunValidator(new CapturingOutput(), client.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ValidateAllAsync("facility-1", hslocMapEnabled: true, ["Patient-abcd1234-001"]));

        ex.Message.Should().Contain("No mapped identifier row");
    }

    private static LinkApiResponse<T> Ok<T>(T body) => new() { StatusCode = 200, Body = body };

    private static LinkApiResponse<PagedConfigModel<FacilityLocationLocalCodeMappingApiModel>> Page(
        params FacilityLocationLocalCodeMappingApiModel[] records) =>
        Ok(new PagedConfigModel<FacilityLocationLocalCodeMappingApiModel>(
            [.. records],
            new PaginationMetadata(100, 1, records.Length)));

    private sealed class CapturingOutput : IAutomationOutput
    {
        public List<string> Lines { get; } = [];
        public void WriteLine(string message) => Lines.Add(message);
        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }
}
