using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Services;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class PatientCensusServiceTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public PatientCensusServiceTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Builds a standard set of 6 EHR patient lists covering all TimeFrame × ListType combinations.
    /// </summary>
    private static List<EhrPatientList> BuildStandardEhrPatientLists(string idPrefix)
    {
        return
        [
            new EhrPatientList { FhirId = $"{idPrefix}-admit-lt24", Status = ListType.Admit, TimeFrame = TimeFrame.LessThan24Hours },
            new EhrPatientList { FhirId = $"{idPrefix}-admit-24-48", Status = ListType.Admit, TimeFrame = TimeFrame.Between24To48Hours },
            new EhrPatientList { FhirId = $"{idPrefix}-admit-gt48", Status = ListType.Admit, TimeFrame = TimeFrame.MoreThan48Hours },
            new EhrPatientList { FhirId = $"{idPrefix}-discharge-lt24", Status = ListType.Discharge, TimeFrame = TimeFrame.LessThan24Hours },
            new EhrPatientList { FhirId = $"{idPrefix}-discharge-24-48", Status = ListType.Discharge, TimeFrame = TimeFrame.Between24To48Hours },
            new EhrPatientList { FhirId = $"{idPrefix}-discharge-gt48", Status = ListType.Discharge, TimeFrame = TimeFrame.MoreThan48Hours },
        ];
    }

    /// <summary>
    /// Seeds FhirListConfiguration + FhirQueryConfiguration into the DB.
    /// Returns the facilityId for reference.
    /// </summary>
    private async Task SeedFhirListAndQueryConfigAsync(DataAcquisitionDbContext dbContext, string facilityId, string fhirBaseUrl = "http://localhost/fhir")
    {
        var listConfig = new FhirListConfiguration
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            FhirBaseServerUrl = fhirBaseUrl,
            EHRPatientLists = BuildStandardEhrPatientLists(facilityId),
        };
        dbContext.FhirListConfigurations.Add(listConfig);

        var queryConfig = new FhirQueryConfiguration
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            FhirServerBaseUrl = fhirBaseUrl,
        };
        dbContext.FhirQueryConfigurations.Add(queryConfig);

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds an SftpConfiguration with census acquisition configs for the facility.
    /// </summary>
    private async Task SeedSftpCensusConfigAsync(DataAcquisitionDbContext dbContext, string facilityId)
    {
        dbContext.SftpConfigurations.Add(new SftpConfiguration
        {
            Id = Guid.NewGuid(),
            OrganizationId = facilityId,
            Host = "sftp.example.com",
            Port = 22,
            AcquisitionConfigurations =
            [
                new SftpAcquisitionTypeConfiguration
                {
                    AcquisitionType = SftpAcquisitionType.Census,
                    SubType = SftpAcquisitionSubType.CernerCCLExtract,
                    RemoteDirectory = "/census",
                    FileNamePattern = "census_*.dat",
                },
            ],
        });

        await dbContext.SaveChangesAsync();
    }

    private PatientCensusService CreateService(
        IServiceScope scope,
        Mock<IReadFhirCommand>? readFhirCommandMock = null,
        Mock<IProducer<string, PatientListMessage>>? kafkaProducerMock = null)
    {
        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var fhirListQueries = scope.ServiceProvider.GetRequiredService<IFhirQueryListConfigurationQueries>();
        var fhirQueryConfigQueries = scope.ServiceProvider.GetRequiredService<IFhirQueryConfigurationQueries>();
        var sftpConfigQueries = new SftpConfigurationQueries(scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>());
        var sftpLogManager = new SftpAcquisitionLogManager(
            new Mock<ILogger<SftpAcquisitionLogManager>>().Object,
            scope.ServiceProvider.GetRequiredService<IDatabase>(),
            scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>());

        return new PatientCensusService(
            new Mock<ILogger<PatientCensusService>>().Object,
            new Mock<IAuthenticationRetrievalService>().Object,
            fhirListQueries,
            fhirQueryConfigQueries,
            (readFhirCommandMock ?? new Mock<IReadFhirCommand>()).Object,
            logManager,
            (kafkaProducerMock ?? new Mock<IProducer<string, PatientListMessage>>()).Object,
            sftpConfigQueries,
            sftpLogManager);
    }

    // ─────────────────────────────────────────────────────────────────
    //  CreateLog – FHIR List path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLog_FhirListPath_CreatesLogWith6Queries()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_FhirList_{tag}";

        await SeedFhirListAndQueryConfigAsync(dbContext, facilityId);

        var service = CreateService(scope);

        await service.CreateLog(facilityId, CancellationToken.None);

        var log = await dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQueries)
                .ThenInclude(q => q.FhirQueryResourceTypes)
            .Where(l => l.FacilityId == facilityId && l.IsCensus)
            .SingleOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Equal(RequestStatus.Pending, log.Status);
        Assert.True(log.IsCensus);
        Assert.Equal(6, log.FhirQueries.Count);

        foreach (var fq in log.FhirQueries)
        {
            Assert.Equal(FhirQueryType.Read, fq.QueryType);
            Assert.NotNull(fq.CensusTimeFrame);
            Assert.NotNull(fq.CensusPatientStatus);
            Assert.False(string.IsNullOrWhiteSpace(fq.CensusListId));
            Assert.Contains(fq.FhirQueryResourceTypes, rt => rt.ResourceType == ResourceType.List);
        }
    }

    [Fact]
    public async Task CreateLog_FhirListPath_Throws_WhenFhirListConfigMissing()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_NoConfig_{tag}";

        var service = CreateService(scope);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => service.CreateLog(facilityId, CancellationToken.None));
        Assert.Contains("Missing census configuration", ex.Message);
    }

    [Fact]
    public async Task CreateLog_FhirListPath_Throws_WhenFhirQueryConfigMissing()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_NoQueryCfg_{tag}";

        dbContext.FhirListConfigurations.Add(new FhirListConfiguration
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            FhirBaseServerUrl = "http://localhost/fhir",
            EHRPatientLists = BuildStandardEhrPatientLists(facilityId),
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(scope);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => service.CreateLog(facilityId, CancellationToken.None));
        Assert.Contains("Missing FHIR query configuration", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────
    //  CreateLog – SFTP Census path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLog_SftpCensusPath_CreatesSftpLogAndSkipsFhirList()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_Sftp_{tag}";

        await SeedSftpCensusConfigAsync(dbContext, facilityId);

        var service = CreateService(scope);

        await service.CreateLog(facilityId, CancellationToken.None);

        // Verify SFTP log was created
        var sftpLogs = await dbContext.SftpAcquisitionLogs
            .Where(l => l.FacilityId == facilityId)
            .ToListAsync();
        Assert.Single(sftpLogs);
        Assert.Equal(SftpAcquisitionType.Census, sftpLogs[0].AcquisitionType);
        Assert.Equal(SftpAcquisitionSubType.CernerCCLExtract, sftpLogs[0].SubType);
        Assert.Equal(RequestStatus.Pending, sftpLogs[0].Status);

        // Verify no FHIR DataAcquisitionLog was created for this facility
        var daLogs = await dbContext.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId)
            .ToListAsync();
        Assert.Empty(daLogs);
    }

    [Fact]
    public async Task CreateLog_SftpConfigExists_ButNoCensusAcq_FallsThroughToFhirList()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_SftpNoCensus_{tag}";

        // Seed SFTP config with only Resources type (no Census)
        dbContext.SftpConfigurations.Add(new SftpConfiguration
        {
            Id = Guid.NewGuid(),
            OrganizationId = facilityId,
            Host = "sftp.example.com",
            Port = 22,
            AcquisitionConfigurations =
            [
                new SftpAcquisitionTypeConfiguration
                {
                    AcquisitionType = SftpAcquisitionType.Resources,
                    SubType = SftpAcquisitionSubType.None,
                },
            ],
        });
        await dbContext.SaveChangesAsync();

        await SeedFhirListAndQueryConfigAsync(dbContext, facilityId);

        var service = CreateService(scope);

        await service.CreateLog(facilityId, CancellationToken.None);

        // Should have fallen through to FHIR List path
        var daLog = await dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQueries)
            .Where(l => l.FacilityId == facilityId && l.IsCensus)
            .SingleOrDefaultAsync();
        Assert.NotNull(daLog);
        Assert.Equal(6, daLog.FhirQueries.Count);

        // No SFTP census log should exist
        var sftpLogs = await dbContext.SftpAcquisitionLogs
            .Where(l => l.FacilityId == facilityId)
            .ToListAsync();
        Assert.Empty(sftpLogs);
    }

    // ─────────────────────────────────────────────────────────────────
    //  RetrieveListData
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetrieveListData_NullLog_ThrowsArgumentNullException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = CreateService(scope);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RetrieveListData(null!, false, CancellationToken.None));
    }

    [Fact]
    public async Task RetrieveListData_WrongQueryCount_ThrowsArgumentException()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = CreateService(scope);

        var log = new DataAcquisitionLogModel
        {
            FacilityId = "SomeFacility",
            FhirQuery = [new FhirQueryModel()], // only 1, not 6
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RetrieveListData(log, false, CancellationToken.None));
    }

    [Fact]
    public async Task RetrieveListData_SuccessfulFhirRead_ReturnsPatientIds_UpdatesLog_ProducesKafka()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_Retrieve_{tag}";

        await SeedFhirListAndQueryConfigAsync(dbContext, facilityId);

        // ── Create census log via service (CreateLog) so the DB is fully consistent.
        var service = CreateService(scope);
        await service.CreateLog(facilityId, CancellationToken.None);

        var logEntity = await dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQueries)
                .ThenInclude(q => q.FhirQueryResourceTypes)
            .Where(l => l.FacilityId == facilityId && l.IsCensus)
            .SingleAsync();

        var logModel = DataAcquisitionLogModel.FromDomain(logEntity);

        // ── Prepare mock that returns a FHIR List with patient entries.
        var readFhirMock = new Mock<IReadFhirCommand>();
        readFhirMock
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadFhirCommandRequest req, CancellationToken _) =>
            {
                var fhirList = new List
                {
                    Id = req.resourceId,
                    Entry =
                    [
                        new List.EntryComponent { Item = new ResourceReference($"Patient/PT-{req.resourceId}-1") },
                        new List.EntryComponent { Item = new ResourceReference($"Patient/PT-{req.resourceId}-2") },
                    ],
                };
                return fhirList;
            });

        var kafkaProducerMock = new Mock<IProducer<string, PatientListMessage>>();
        kafkaProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, PatientListMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, PatientListMessage>());

        var retrieveService = CreateService(scope, readFhirMock, kafkaProducerMock);

        // Act
        var results = await retrieveService.RetrieveListData(logModel, triggerMessage: true, CancellationToken.None);

        // Assert — 6 lists, each with 2 patients
        Assert.Equal(6, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal(2, r.PatientIds.Count);
            Assert.All(r.PatientIds, pid => Assert.StartsWith("PT-", pid));
        });

        // Assert — log updated to Completed in DB
        var updatedLog = await logQueries.GetAsync(logEntity.Id);
        Assert.Equal(RequestStatus.Completed, updatedLog!.Status);
        Assert.NotNull(updatedLog.CompletionDate);
        Assert.True(updatedLog.CompletionTimeMilliseconds >= 0);

        // Assert — Kafka message produced exactly once
        kafkaProducerMock.Verify(p => p.ProduceAsync(
            It.Is<string>(topic => topic == "PatientListsAcquired"),
            It.Is<Message<string, PatientListMessage>>(m =>
                m.Key == facilityId &&
                m.Value.PatientLists.Count == 6),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveListData_FhirReadFails_MarksLogFailed_ThrowsWhenTriggerMessage()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_Fail_{tag}";

        await SeedFhirListAndQueryConfigAsync(dbContext, facilityId);

        var service = CreateService(scope);
        await service.CreateLog(facilityId, CancellationToken.None);

        var logEntity = await dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQueries)
                .ThenInclude(q => q.FhirQueryResourceTypes)
            .Where(l => l.FacilityId == facilityId && l.IsCensus)
            .SingleAsync();
        var logModel = DataAcquisitionLogModel.FromDomain(logEntity);

        // FHIR call always throws
        var readFhirMock = new Mock<IReadFhirCommand>();
        readFhirMock
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("FHIR server timed out"));

        var failService = CreateService(scope, readFhirMock);

        // Act – with triggerMessage = true, should throw after updating log
        var ex = await Assert.ThrowsAsync<Exception>(
            () => failService.RetrieveListData(logModel, triggerMessage: true, CancellationToken.None));
        Assert.Contains("Failed to retrieve patient list", ex.Message);

        // Assert — log updated to Failed in DB
        var updatedLog = await logQueries.GetAsync(logEntity.Id);
        Assert.Equal(RequestStatus.Failed, updatedLog!.Status);
    }

    [Fact]
    public async Task RetrieveListData_FhirReadFails_NoTrigger_ReturnsEmptyAndMarksLogFailed()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_FailNoMsg_{tag}";

        await SeedFhirListAndQueryConfigAsync(dbContext, facilityId);

        var service = CreateService(scope);
        await service.CreateLog(facilityId, CancellationToken.None);

        var logEntity = await dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQueries)
                .ThenInclude(q => q.FhirQueryResourceTypes)
            .Where(l => l.FacilityId == facilityId && l.IsCensus)
            .SingleAsync();
        var logModel = DataAcquisitionLogModel.FromDomain(logEntity);

        var readFhirMock = new Mock<IReadFhirCommand>();
        readFhirMock
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection refused"));

        var failService = CreateService(scope, readFhirMock);

        // Act – with triggerMessage = false, should NOT throw
        var results = await failService.RetrieveListData(logModel, triggerMessage: false, CancellationToken.None);

        Assert.Empty(results);

        var updatedLog = await logQueries.GetAsync(logEntity.Id);
        Assert.Equal(RequestStatus.Failed, updatedLog!.Status);
    }

    // ─────────────────────────────────────────────────────────────────
    //  End-to-end: CreateLog → DB → RetrieveListData → Kafka
    //  This covers the full census pipeline a real report would exercise.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_CreateLog_ThenRetrieveListData_ProducesCorrectKafkaPayload()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var facilityId = $"CensusTest_E2E_{tag}";

        await SeedFhirListAndQueryConfigAsync(dbContext, facilityId);

        // ── Step 1: CreateLog writes a census DA log with 6 FHIR queries to the DB.
        var createService = CreateService(scope);
        await createService.CreateLog(facilityId, CancellationToken.None);

        // ── Step 2: Read the log back from the DB (like AcquisitionProcessingJob would).
        var logEntity = await dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQueries)
                .ThenInclude(q => q.FhirQueryResourceTypes)
            .Where(l => l.FacilityId == facilityId && l.IsCensus)
            .SingleAsync();
        var logModel = DataAcquisitionLogModel.FromDomain(logEntity);

        // ── Step 3: Mock FHIR reads returning different patients per list.
        var readFhirMock = new Mock<IReadFhirCommand>();
        int callIndex = 0;
        readFhirMock
            .Setup(r => r.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadFhirCommandRequest req, CancellationToken _) =>
            {
                var idx = Interlocked.Increment(ref callIndex);
                return new List
                {
                    Id = req.resourceId,
                    Entry =
                    [
                        new List.EntryComponent { Item = new ResourceReference($"Patient/E2E-{idx}-001") },
                        new List.EntryComponent { Item = new ResourceReference($"Patient/E2E-{idx}-002") },
                        new List.EntryComponent { Item = new ResourceReference($"Patient/E2E-{idx}-003") },
                    ],
                };
            });

        Message<string, PatientListMessage>? capturedMessage = null;
        var kafkaProducerMock = new Mock<IProducer<string, PatientListMessage>>();
        kafkaProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, PatientListMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, PatientListMessage>, CancellationToken>((_, msg, _) => capturedMessage = msg)
            .ReturnsAsync(new DeliveryResult<string, PatientListMessage>());

        // ── Step 4: RetrieveListData processes log, calls FHIR, updates DB, produces Kafka.
        var retrieveService = CreateService(scope, readFhirMock, kafkaProducerMock);
        var results = await retrieveService.RetrieveListData(logModel, triggerMessage: true, CancellationToken.None);

        // ── Assertions on returned results
        Assert.Equal(6, results.Count);
        Assert.All(results, r => Assert.Equal(3, r.PatientIds.Count));

        // Verify we have both Admit and Discharge list types
        var admitLists = results.Where(r => r.ListType == LantanaGroup.Link.Shared.Application.Models.DataAcq.ListType.Admit).ToList();
        var dischargeLists = results.Where(r => r.ListType == LantanaGroup.Link.Shared.Application.Models.DataAcq.ListType.Discharge).ToList();
        Assert.Equal(3, admitLists.Count);
        Assert.Equal(3, dischargeLists.Count);

        // Verify all three timeframes are covered for each type
        var admitTimeFrames = admitLists.Select(r => r.TimeFrame).ToHashSet();
        Assert.Contains(LantanaGroup.Link.Shared.Application.Models.DataAcq.TimeFrame.LessThan24Hours, admitTimeFrames);
        Assert.Contains(LantanaGroup.Link.Shared.Application.Models.DataAcq.TimeFrame.Between24To48Hours, admitTimeFrames);
        Assert.Contains(LantanaGroup.Link.Shared.Application.Models.DataAcq.TimeFrame.MoreThan48Hours, admitTimeFrames);

        // ── Assertions on DB state
        var updatedLog = await logQueries.GetAsync(logEntity.Id);
        Assert.Equal(RequestStatus.Completed, updatedLog!.Status);
        Assert.NotNull(updatedLog.CompletionDate);

        // ── Assertions on Kafka message
        Assert.NotNull(capturedMessage);
        Assert.Equal(facilityId, capturedMessage!.Key);
        Assert.Equal(6, capturedMessage.Value.PatientLists.Count);
        var allPatientIds = capturedMessage.Value.PatientLists.SelectMany(pl => pl.PatientIds).ToList();
        Assert.Equal(18, allPatientIds.Count); // 6 lists × 3 patients
        Assert.All(allPatientIds, pid => Assert.StartsWith("E2E-", pid));
    }
}
