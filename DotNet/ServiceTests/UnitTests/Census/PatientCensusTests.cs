using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Census;

[Collection("UnitTests")]
public class PatientCensusTests
{
    [Fact]
    public async Task GetAllPatientsForFacilityQuery_Success()
    {
        // Arrange
        var mockManager = new Mock<ICensusPatientListManager>();
        var mockRepo = new Mock<IBaseEntityRepository<CensusPatientListEntity>>();
        var mockPatientList = new List<CensusPatientListEntity>
        {
            new CensusPatientListEntity
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        mockManager.Setup(x => x.GetPatientList(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(mockPatientList);

        // Act
        var result = (await mockManager.Object.GetPatientList("123", DateTime.Now, DateTime.Now)).ToList();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("123", result[0].FacilityId);
        Assert.Equal("456", result[0].PatientId);
        Assert.False(result[0].IsDischarged);
    }


    [Fact]
    public async Task GetCurrentCensusQueryHandler_Success()
    {
        // Arrange
        var mockManager = new Mock<ICensusPatientListManager>();
        var mockRepo = new Mock<IBaseEntityRepository<CensusPatientListEntity>>();
        var mockPatientList = new List<CensusPatientListEntity>
        {
            new CensusPatientListEntity
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        mockManager.Setup(x => x.GetPatientListForFacility(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockPatientList);

        // Act
        var result = await mockManager.Object.GetPatientListForFacility("123", activeOnly: true, CancellationToken.None);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal("123", result[0].FacilityId);
        Assert.Equal("456", result[0].PatientId);
        Assert.False(result[0].IsDischarged);
    }


    [Fact]
    public async Task GetCensusHistoryQueryHandler_Success()
    {
        // Arrange
        var mockManager = new Mock<IPatientCensusHistoryManager>();
        var mockHistoryList = new List<PatientCensusHistoricEntity>
        {
            new PatientCensusHistoricEntity
            {
                FacilityId = "123",
                CensusDateTime = DateTime.Parse("12/6/2023 10:34:28 PM"),
                ReportId = "456"
            }
        };

        mockManager.Setup(x => x.GetPatientCensusHistoryByFacilityId(It.IsAny<string>())).Returns(Task.FromResult(mockHistoryList.AsEnumerable()));

        // Act
        var result = (await mockManager.Object.GetPatientCensusHistoryByFacilityId("123")).ToList();
        var expectedReportId = "123-12/06/2023 10:34:28 PM";
        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("123", result[0].FacilityId);
    }

    [Fact]
    public async Task ConsumePatientIdsAcquiredEventHandler_Success_AddOnePatient()
    {
        //setup
        var mockPatientListRepo = new Mock<ICensusPatientListManager>();
        var mockHistoryRepo = new Mock<IPatientCensusHistoryManager>();
        var metrics = new Mock<ICensusServiceMetrics>();
        var logger = new Mock<ILogger<PatientIdsAcquiredService>>();

        var existingPatientList = new List<CensusPatientListEntity>
        {
            new CensusPatientListEntity
            {
                FacilityId = "123",
                PatientId = "Patient/456",
                IsDischarged = false
            }
        };
        var patientIdsAcquired = new PatientIDsAcquired
        {
            PatientIds = new List()
        };
        patientIdsAcquired.PatientIds.Code = new CodeableConcept
        {
            Text = "PatientList"
        };
        patientIdsAcquired.PatientIds.Entry.Add(new List.EntryComponent
        {
            Item = new ResourceReference
            {
                Reference = "Patient/456",
                Display = "Patient 456"
            }
        });

        mockPatientListRepo.Setup(x => x.GetPatientListForFacility(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingPatientList);
        mockPatientListRepo.Setup(x => x.GetPatientByPatientId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusPatientListEntity
        {
            FacilityId = "123",
            PatientId = "456",
            IsDischarged = false
        });

        mockPatientListRepo.Setup(x => x.UpdateAsync(It.IsAny<CensusPatientListEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusPatientListEntity());
        mockHistoryRepo.Setup(x => x.AddAsync(It.IsAny<PatientCensusHistoricEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PatientCensusHistoricEntity());

        var service = new PatientIdsAcquiredService(logger.Object, mockPatientListRepo.Object, mockHistoryRepo.Object, metrics.Object);

        var eventList = await service.ProcessEvent(new ConsumePatientIdsAcquiredEventModel
        {
            FacilityId = "123",
            Message = patientIdsAcquired
        }, CancellationToken.None);

        Assert.True(eventList.Count() == 1);
    }

    [Fact]
    public async Task ConsumePatientIdsAcquiredEventHandler_Success_DischargePatient()
    {
        //setup
        var mockPatientListRepo = new Mock<ICensusPatientListManager>();
        var mockHistoryRepo = new Mock<IPatientCensusHistoryManager>();
        var logger = new Mock<ILogger<PatientIdsAcquiredService>>();
        var mockMetrics = new Mock<ICensusServiceMetrics>();

        var existingPatientList = new List<CensusPatientListEntity>
        {
            new CensusPatientListEntity
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        var patientIdsAcquired = new PatientIDsAcquired
        {
            PatientIds = new List()
        };
        patientIdsAcquired.PatientIds.Code = new CodeableConcept
        {
            Text = "PatientList"
        };
        patientIdsAcquired.PatientIds.Entry.Add(new List.EntryComponent
        {
            Item = new ResourceReference
            {
                Reference = "Patient/789",
                Display = "Patient 789"
            }
        });

        mockPatientListRepo.Setup(x => x.GetPatientListForFacility(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingPatientList);
        mockPatientListRepo.Setup(x => x.GetPatientByPatientId(It.IsAny<string>(), It.Is<string>(x => x == "456"), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusPatientListEntity
        {
            FacilityId = "123",
            PatientId = "456",
            IsDischarged = false
        });

        mockPatientListRepo.Setup(x => x.UpdateAsync(It.IsAny<CensusPatientListEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusPatientListEntity());
        mockHistoryRepo.Setup(x => x.AddAsync(It.IsAny<PatientCensusHistoricEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PatientCensusHistoricEntity());

        var handler = new PatientIdsAcquiredService(logger.Object, mockPatientListRepo.Object, mockHistoryRepo.Object, mockMetrics.Object);
        var eventList = await handler.ProcessEvent(new ConsumePatientIdsAcquiredEventModel
        {
            FacilityId = "123",
            Message = patientIdsAcquired
        }, CancellationToken.None);

        Assert.Single(eventList);
    }
}
