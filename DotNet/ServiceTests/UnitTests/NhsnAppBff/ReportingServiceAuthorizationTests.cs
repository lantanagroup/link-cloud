using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Reporting;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.NhsnAppBff;

[Trait("Category", "UnitTests")]
public sealed class ReportingServiceAuthorizationTests
{
    private const string CurrentFacilityId = "facility-a";

    [Fact]
    public async Task GetReportPatientsAsync_WhenReportBelongsToAnotherFacility_ThrowsNotFoundAndDoesNotReadPatients()
    {
        var reportGateway = new Mock<IReportGateway>(MockBehavior.Strict);
        reportGateway
            .Setup(gateway => gateway.GetReportFacilityIdAsync("foreign-report", It.IsAny<CancellationToken>()))
            .ReturnsAsync("facility-b");

        var service = CreateService(reportGateway);

        await Assert.ThrowsAsync<ReportNotFoundException>(() => service.GetReportPatientsAsync("foreign-report"));

        reportGateway.Verify(
            gateway => gateway.GetReportPatientsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPatientPreQualResultsAsync_WhenReportBelongsToCurrentFacility_ForwardsCurrentFacilityOnlyAfterOwnershipCheck()
    {
        var reportGateway = new Mock<IReportGateway>(MockBehavior.Strict);
        reportGateway
            .Setup(gateway => gateway.GetReportFacilityIdAsync("current-report", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrentFacilityId);

        var validationGateway = new Mock<IValidationGateway>(MockBehavior.Strict);
        validationGateway
            .Setup(gateway => gateway.GetPatientResultsAsync(CurrentFacilityId, "current-report", "patient-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService(reportGateway, validationGateway: validationGateway);

        var result = await service.GetPatientPreQualResultsAsync("current-report", "patient-1");

        Assert.Empty(result);
        reportGateway.Verify(gateway => gateway.GetReportFacilityIdAsync("current-report", It.IsAny<CancellationToken>()), Times.Once);
        validationGateway.VerifyAll();
    }

    private static ReportingService CreateService(
        Mock<IReportGateway> reportGateway,
        Mock<IValidationGateway>? validationGateway = null)
    {
        var userContext = new Mock<INhsnUserContext>(MockBehavior.Strict);
        userContext.Setup(context => context.RequireFacilityId()).Returns(CurrentFacilityId);

        return new ReportingService(
            new Mock<IFacilityGateway>(MockBehavior.Loose).Object,
            reportGateway.Object,
            new Mock<IReportBlobStorageClient>(MockBehavior.Loose).Object,
            new Mock<IDataAcquisitionGateway>(MockBehavior.Loose).Object,
            new Mock<IAcknowledgementService>(MockBehavior.Loose).Object,
            (validationGateway ?? new Mock<IValidationGateway>(MockBehavior.Loose)).Object,
            userContext.Object,
            NullLogger<ReportingService>.Instance);
    }
}