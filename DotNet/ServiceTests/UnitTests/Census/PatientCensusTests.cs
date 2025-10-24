using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Messages;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Census;

[Trait("Category", "UnitTests")]
public class PatientCensusTests
{
    [Fact]
    public async Task GetAllPatientsForFacilityQuery_Success()
    {
        // Arrange
        var mockQueries = new Mock<ICensusPatientListQueries>();
        var mockPatientList = new List<CensusPatientListModel>
        {
            new CensusPatientListModel
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        mockQueries.Setup(x => x.SearchAsync(It.Is<SearchCensusPatientListModel>(m => m.FacilityId == "123" && m.AdmitDateStart.HasValue && m.AdmitDateEnd.HasValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<CensusPatientListModel> { Records = mockPatientList });

        // Act
        var model = new SearchCensusPatientListModel
        {
            FacilityId = "123",
            AdmitDateStart = DateTime.Now.AddDays(-1), // Example dates
            AdmitDateEnd = DateTime.Now,
            PageSize = int.MaxValue
        };
        var result = (await mockQueries.Object.SearchAsync(model)).Records;

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
        var mockQueries = new Mock<ICensusPatientListQueries>();
        var mockPatientList = new List<CensusPatientListModel>
        {
            new CensusPatientListModel
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        mockQueries.Setup(x => x.SearchAsync(It.Is<SearchCensusPatientListModel>(m => m.FacilityId == "123" && m.ActiveOnly), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<CensusPatientListModel> { Records = mockPatientList });

        // Act
        var model = new SearchCensusPatientListModel
        {
            FacilityId = "123",
            ActiveOnly = true,
            PageSize = int.MaxValue
        };
        var result = (await mockQueries.Object.SearchAsync(model)).Records;

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
        var mockManager = new Mock<ICensusPatientListManager>();
        var mockQueries = new Mock<ICensusPatientListQueries>();
        var mockHistoryRepo = new Mock<IPatientCensusHistoryManager>();
        var metrics = new Mock<ICensusServiceMetrics>();
        var logger = new Mock<ILogger<PatientIdsAcquiredService>>();

        mockQueries.Setup(x => x.SearchAsync(It.Is<SearchCensusPatientListModel>(m => m.FacilityId == "123" && m.ActiveOnly), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<CensusPatientListModel> { Records = new List<CensusPatientListModel>() });

        mockQueries.Setup(x => x.SearchAsync(It.Is<SearchCensusPatientListModel>(m => m.FacilityId == "123" && m.PatientId == "456"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<CensusPatientListModel> { Records = new List<CensusPatientListModel>() });

        mockQueries.Setup(x => x.GetAsync("123", "456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CensusPatientListModel)null);

        mockManager.Setup(x => x.CreateAsync(It.IsAny<CreateCensusPatientListModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensusPatientListModel());

        mockHistoryRepo.Setup(x => x.AddAsync(It.IsAny<PatientCensusHistoricEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PatientCensusHistoricEntity());

        var service = new PatientIdsAcquiredService(logger.Object, mockManager.Object, mockHistoryRepo.Object, mockQueries.Object, metrics.Object);

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

        var eventList = await service.ProcessEvent(new ConsumePatientIdsAcquiredEventModel
        {
            FacilityId = "123",
            Message = patientIdsAcquired
        }, CancellationToken.None);

        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ConsumePatientIdsAcquiredEventHandler_Success_DischargePatient()
    {
        //setup
        var mockManager = new Mock<ICensusPatientListManager>();
        var mockQueries = new Mock<ICensusPatientListQueries>();
        var mockHistoryRepo = new Mock<IPatientCensusHistoryManager>();
        var logger = new Mock<ILogger<PatientIdsAcquiredService>>();
        var mockMetrics = new Mock<ICensusServiceMetrics>();

        var existingPatientList = new List<CensusPatientListModel>
        {
            new CensusPatientListModel
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        mockQueries.Setup(x => x.SearchAsync(It.Is<SearchCensusPatientListModel>(m => m.FacilityId == "123" && m.ActiveOnly), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<CensusPatientListModel> { Records = existingPatientList });

        mockQueries.Setup(x => x.SearchAsync(It.Is<SearchCensusPatientListModel>(m => m.FacilityId == "123" && m.PatientId == "789"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<CensusPatientListModel> { Records = new List<CensusPatientListModel>() });

        mockManager.Setup(x => x.UpdateAsync(It.Is<UpdateCensusPatientListModel>(m => m.PatientId == "456" && m.IsDischarged), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatientList[0]);

        mockManager.Setup(x => x.CreateAsync(It.Is<CreateCensusPatientListModel>(m => m.PatientId == "789"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CensusPatientListModel { PatientId = "789" });

        mockHistoryRepo.Setup(x => x.AddAsync(It.IsAny<PatientCensusHistoricEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PatientCensusHistoricEntity());

        var handler = new PatientIdsAcquiredService(logger.Object, mockManager.Object, mockHistoryRepo.Object, mockQueries.Object, mockMetrics.Object);
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

        var eventList = await handler.ProcessEvent(new ConsumePatientIdsAcquiredEventModel
        {
            FacilityId = "123",
            Message = patientIdsAcquired
        }, CancellationToken.None);

        Assert.Single(eventList);
    }
}