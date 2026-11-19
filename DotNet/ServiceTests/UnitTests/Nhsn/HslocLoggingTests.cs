using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Hsloc;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Nhsn;

[Trait("Category", "UnitTests")]
public class HslocLoggingTests
{
    [Fact]
    public async Task SaveAsync_SanitizesRejectedCodesInLogs_WithoutChangingTheFacilityQuery()
    {
        const string facilityId = "facility\r\nforged";
        var userContext = new Mock<INhsnUserContext>();
        userContext.Setup(c => c.RequireFacilityId()).Returns(facilityId);
        using var cancellation = new CancellationTokenSource();
        var client = new Mock<INormalizationServiceClient>(MockBehavior.Strict);
        client.Setup(c => c.GetHslocCodesAsync(false, cancellation.Token))
            .ReturnsAsync(new LinkApiResponse<List<HslocCodeApiModel>> { StatusCode = 200, RawBody = "[]", Body = [] });
        client.Setup(c => c.SearchFacilityLocationLocalCodeMappingsAsync(
                It.Is<SearchFacilityLocationLocalCodeMappingsRequestApiModel>(r => r.FacilityId == facilityId),
                cancellation.Token))
            .ReturnsAsync(new LinkApiResponse<PagedConfigModel<FacilityLocationLocalCodeMappingApiModel>>
            {
                StatusCode = 200,
                Body = new PagedConfigModel<FacilityLocationLocalCodeMappingApiModel> { Records = [] }
            });
        var logger = new Mock<ILogger<HslocMappingService>>();
        var mapping = new HslocMapping { SourceCode = "local-code", HslocCode = "unknown\r\nforged" };
        var service = new HslocMappingService(client.Object, userContext.Object, logger.Object);

        await service.SaveAsync([mapping], cancellation.Token);

        var entry = Assert.Single(logger.Invocations, i => i.Method.Name == nameof(ILogger.Log));
        var message = entry.Arguments[2].ToString()!;
        Assert.DoesNotContain(message, char.IsControl);
        Assert.Contains("unknown  forged", message);
        Assert.Equal("unknown\r\nforged", mapping.HslocCode);
        client.VerifyAll();
        client.VerifyNoOtherCalls();
    }
}
