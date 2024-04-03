
using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Commands;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Models.Messages;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace CensusUnitTests;
public class PatientCensusTests
{
    [Fact]
    public async Task GetAllPatientsForFacilityQuery_Success() 
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GetAllPatientsForFacilityQueryHandler>>();
        var mockRepo = new Mock<ICensusPatientListRepository>();
        var mockPatientList = new List<CensusPatientListEntity>
        {
            new CensusPatientListEntity
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        mockRepo.Setup(x => x.GetAllPatientsForFacility(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockPatientList);
        var handler = new GetAllPatientsForFacilityQueryHandler(mockLogger.Object, mockRepo.Object);
        var query = new GetAllPatientsForFacilityQuery { FacilityId = "123" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("123", result[0].FacilityId);
        Assert.Equal("456", result[0].PatientId);
        Assert.False(result[0].IsDischarged);
    }

    [Fact]
    public async Task GetAllPatientsForFacilityQuery_MongoFail() 
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GetAllPatientsForFacilityQueryHandler>>();
        var mockRepo = new Mock<ICensusPatientListRepository>();
        mockRepo.Setup(x => x.GetAllPatientsForFacility(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Mongo failed."));
        var handler = new GetAllPatientsForFacilityQueryHandler(mockLogger.Object, mockRepo.Object);
        var query = new GetAllPatientsForFacilityQuery { FacilityId = "123" };

        // Act
        try
        {
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task GetCurrentCensusQueryHandler_Success() 
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GetCurrentCensusQueryHandler>>();
        var mockRepo = new Mock<ICensusPatientListRepository>();
        var mockPatientList = new List<CensusPatientListEntity>
        {
            new CensusPatientListEntity
            {
                FacilityId = "123",
                PatientId = "456",
                IsDischarged = false
            }
        };
        mockRepo.Setup(x => x.GetActivePatientsForFacility(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(mockPatientList);
        var handler = new GetCurrentCensusQueryHandler(mockLogger.Object, mockRepo.Object);
        var query = new GetCurrentCensusQuery { FacilityId = "123" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Count);
        Assert.Equal("123", result[0].FacilityId);
        Assert.Equal("456", result[0].PatientId);
        Assert.False(result[0].IsDischarged);
    }

    [Fact]
    public async Task GetCurrentCensusQueryHandler_MongoFail() 
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GetCurrentCensusQueryHandler>>();
        var mockRepo = new Mock<ICensusPatientListRepository>();
        mockRepo.Setup(x => x.GetActivePatientsForFacility(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Mongo failed."));
        var handler = new GetCurrentCensusQueryHandler(mockLogger.Object, mockRepo.Object);
        var query = new GetCurrentCensusQuery { FacilityId = "123" };

        // Act
        try
        {
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task GetCensusHistoryQueryHandler_Success() 
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GetCensusHistoryQueryHandler>>();
        var mockRepo = new Mock<ICensusHistoryRepository>();
        var mockHistoryList = new List<PatientCensusHistoricEntity>
        {
            new PatientCensusHistoricEntity
            {
                FacilityId = "123",
                CensusDateTime = DateTime.Parse("12/6/2023 10:34:28 PM"),
                ReportId = "456"
            }
        };
        mockRepo.Setup(x => x.GetAllCensusReportsForFacility(It.IsAny<string>())).Returns(mockHistoryList);
        var handler = new GetCensusHistoryQueryHandler(mockLogger.Object, mockRepo.Object);
        var query = new GetCensusHistoryQuery { FacilityId = "123" };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);
        var expectedReportId = "123-12/06/2023 10:34:28 PM";
        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("123", result[0].FacilityId);
    }

    [Fact] 
    public async Task GetCensusHistoryQueryHandler_MongoFail() 
    {
        // Arrange
        var mockLogger = new Mock<ILogger<GetCensusHistoryQueryHandler>>();
        var mockRepo = new Mock<ICensusHistoryRepository>();
        mockRepo.Setup(x => x.GetAllCensusReportsForFacility(It.IsAny<string>())).Throws(new Exception("Mongo failed."));
        var handler = new GetCensusHistoryQueryHandler(mockLogger.Object, mockRepo.Object);
        var query = new GetCensusHistoryQuery { FacilityId = "123" };

        // Act
        try
        {
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.True(false);
        }
        catch (Exception)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task ConsumePatientIdsAcquiredEventHandler_Success_AddOnePatient()
    {
        //setup
        var mockLogger = new Mock<ILogger<ConsumePaitentIdsAcquiredEventHandler>>();
        var mockPatientListRepo = new Mock<ICensusPatientListRepository>();
        var mockHistoryRepo = new Mock<ICensusHistoryRepository>();

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

        mockPatientListRepo.Setup(x => x.GetActivePatientsForFacility(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingPatientList);
        mockPatientListRepo.Setup(x => x.UpdateAsync(It.IsAny<CensusPatientListEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusPatientListEntity());
        mockHistoryRepo.Setup(x => x.AddAsync(It.IsAny<PatientCensusHistoricEntity>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new ConsumePaitentIdsAcquiredEventHandler(mockLogger.Object, mockPatientListRepo.Object, mockHistoryRepo.Object);
        var eventList = await handler.Handle(new ConsumePatientIdsAcquiredEventCommand
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
        var mockLogger = new Mock<ILogger<ConsumePaitentIdsAcquiredEventHandler>>();
        var mockPatientListRepo = new Mock<ICensusPatientListRepository>();
        var mockHistoryRepo = new Mock<ICensusHistoryRepository>();

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

        mockPatientListRepo.Setup(x => x.GetActivePatientsForFacility(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingPatientList);
        mockPatientListRepo.Setup(x => x.UpdateAsync(It.IsAny<CensusPatientListEntity>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CensusPatientListEntity());
        mockHistoryRepo.Setup(x => x.AddAsync(It.IsAny<PatientCensusHistoricEntity>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new ConsumePaitentIdsAcquiredEventHandler(mockLogger.Object, mockPatientListRepo.Object, mockHistoryRepo.Object);
        var eventList = await handler.Handle(new ConsumePatientIdsAcquiredEventCommand
        {
            FacilityId = "123",
            Message = patientIdsAcquired
        }, CancellationToken.None);

        Assert.Single(eventList);
    }
}
