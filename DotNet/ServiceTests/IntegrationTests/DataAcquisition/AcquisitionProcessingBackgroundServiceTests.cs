using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class AcquisitionProcessorBackgroundServiceTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;
    private readonly Mock<IPatientDataService> _patientDataServiceMock;

    public AcquisitionProcessorBackgroundServiceTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _patientDataServiceMock = new Mock<IPatientDataService>();
    }



    [Fact]
    public async Task ProcessWorkItem_LogNotFound_SkipsProcessing()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AcquisitionProcessorBackgroundService>>();
        var service = new AcquisitionProcessorBackgroundService(loggerMock.Object, _fixture.ServiceProvider, null);

        // Use an ID that definitely does not exist
        var workItem = new AcquisitionWorkItem(999999, "NonExistent");

        // Act
        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await service.EnqueueAsync(workItem, cts.Token);

        // Wait long enough for the channel consumer to attempt processing
        await Task.Delay(1000);

        _patientDataServiceMock.Verify(s =>
            s.ExecuteLogRequest(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await service.StopAsync(CancellationToken.None);
    }
}