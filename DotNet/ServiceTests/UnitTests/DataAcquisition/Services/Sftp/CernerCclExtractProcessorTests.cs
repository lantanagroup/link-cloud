using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services.Sftp;

[Trait("Category", "UnitTests")]
public class CernerCclExtractProcessorTests
{
    private readonly Mock<ISftpClientService> _sftpClientServiceMock;
    private readonly Mock<ISftpSession> _sessionMock;
    private readonly Mock<IFileParserFactory> _parserFactoryMock;
    private readonly Mock<IProducer<string, CernerPatientsAcquired>> _kafkaProducerMock;
    private readonly CernerCclExtractProcessor _processor;

    // Shared test data
    private readonly SftpAcquisitionLog _testLog;
    private readonly SftpConfigurationModel _testSftpConfig;
    private readonly SftpAcquisitionTypeConfiguration _testAcquisitionConfig;

    public CernerCclExtractProcessorTests()
    {
        var logger = new Mock<ILogger<CernerCclExtractProcessor>>().Object;
        _sftpClientServiceMock = new Mock<ISftpClientService>();
        _sessionMock = new Mock<ISftpSession>();
        _parserFactoryMock = new Mock<IFileParserFactory>();
        _kafkaProducerMock = new Mock<IProducer<string, CernerPatientsAcquired>>();

        _processor = new CernerCclExtractProcessor(
            logger,
            _sftpClientServiceMock.Object,
            _parserFactoryMock.Object,
            _kafkaProducerMock.Object);

        _testLog = new SftpAcquisitionLog
        {
            Id = 1,
            FacilityId = "TestFacility",
            AcquisitionType = SftpAcquisitionType.Census,
            SubType = SftpAcquisitionSubType.CernerCCLExtract
        };

        _testSftpConfig = new SftpConfigurationModel
        {
            OrganizationId = "TestFacility",
            Host = "sftp.example.com",
            Port = 22,
            RemoteDirectory = "/data",
            RemoveAfterProcessing = false
        };

        _testAcquisitionConfig = new SftpAcquisitionTypeConfiguration
        {
            AcquisitionType = SftpAcquisitionType.Census,
            SubType = SftpAcquisitionSubType.CernerCCLExtract,
            RemoteDirectory = "/data/census",
            FileNamePattern = "*.dat",
            ProcessedDirectory = "/data/processed"
        };
    }

    #region CanProcess

    [Fact]
    public void CanProcess_CensusCernerCclExtract_ReturnsTrue()
    {
        Assert.True(_processor.CanProcess(SftpAcquisitionType.Census, SftpAcquisitionSubType.CernerCCLExtract));
    }

    [Theory]
    [InlineData(SftpAcquisitionType.Resources, SftpAcquisitionSubType.CernerCCLExtract)]
    [InlineData(SftpAcquisitionType.Census, SftpAcquisitionSubType.None)]
    public void CanProcess_WrongTypeOrSubType_ReturnsFalse(
        SftpAcquisitionType type, SftpAcquisitionSubType subType)
    {
        Assert.False(_processor.CanProcess(type, subType));
    }

    #endregion

    #region ProcessWithSessionAsync - Happy path

    [Fact]
    public async Task ProcessWithSessionAsync_SingleFileWithEncounters_ProducesKafkaEventAndMovesFile()
    {
        // Arrange
        var files = new List<SftpFileInfo>
        {
            new() { Name = "census_20230707.dat", FullName = "/data/census/census_20230707.dat" }
        };

        var fileContent = "12345|67890|Fac|Unit|101|A|FIN001|MRN001|Doe|Active|IP|20230707130643|";
        var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));

        SetupSessionListFiles(files);
        SetupSessionDownload("/data/census/census_20230707.dat", fileStream);
        SetupRealCernerParser();

        // Act
        var result = await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, _testSftpConfig, _testAcquisitionConfig, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("census_20230707.dat", result[0]);

        // Verify Kafka event was produced with correct topic and key
        _kafkaProducerMock.Verify(p => p.ProduceAsync(
            nameof(KafkaTopic.CernerPatientsAcquired),
            It.Is<Message<string, CernerPatientsAcquired>>(m =>
                m.Key == "TestFacility" &&
                m.Value.PatientEncounters.Count == 1 &&
                m.Value.PatientEncounters[0].PatientId == "12345"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify file was moved to processed directory
        _sessionMock.Verify(s => s.MoveFileAsync(
            "/data/census/census_20230707.dat",
            "/data/processed",
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify producer was flushed
        _kafkaProducerMock.Verify(p => p.Flush(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWithSessionAsync_MultipleFiles_ProducesOneKafkaEventPerFile()
    {
        // Arrange
        var files = new List<SftpFileInfo>
        {
            new() { Name = "census_day1.dat", FullName = "/data/census/census_day1.dat" },
            new() { Name = "census_day2.dat", FullName = "/data/census/census_day2.dat" }
        };

        var content1 = "11111|22222|Fac|U|1|A|F1|M1|N1|Active|IP|20230101120000|";
        var content2 = "33333|44444|Fac|U|2|B|F2|M2|N2|Active|IP|20230201120000|";

        SetupSessionListFiles(files);
        SetupSessionDownload("/data/census/census_day1.dat", CreateStream(content1));
        SetupSessionDownload("/data/census/census_day2.dat", CreateStream(content2));
        SetupRealCernerParser();

        // Act
        var result = await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, _testSftpConfig, _testAcquisitionConfig, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);

        _kafkaProducerMock.Verify(p => p.ProduceAsync(
            nameof(KafkaTopic.CernerPatientsAcquired),
            It.IsAny<Message<string, CernerPatientsAcquired>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    #endregion

    #region ProcessWithSessionAsync - No files

    [Fact]
    public async Task ProcessWithSessionAsync_NoFilesFound_ReturnsEmptyAndNoKafkaEvents()
    {
        SetupSessionListFiles(new List<SftpFileInfo>());

        var result = await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, _testSftpConfig, _testAcquisitionConfig, CancellationToken.None);

        Assert.Empty(result);

        _kafkaProducerMock.Verify(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, CernerPatientsAcquired>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _kafkaProducerMock.Verify(p => p.Flush(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ProcessWithSessionAsync - Post-processing strategies

    [Fact]
    public async Task ProcessWithSessionAsync_RemoveAfterProcessing_DeletesFileInsteadOfMoving()
    {
        var files = new List<SftpFileInfo>
        {
            new() { Name = "test.dat", FullName = "/data/census/test.dat" }
        };
        var content = "12345|67890|Fac|U|1|A|F|M|N|Active|IP|20230101120000|";

        SetupSessionListFiles(files);
        SetupSessionDownload("/data/census/test.dat", CreateStream(content));
        SetupRealCernerParser();

        // No ProcessedDirectory, but RemoveAfterProcessing = true
        var configNoProcessedDir = new SftpAcquisitionTypeConfiguration
        {
            AcquisitionType = SftpAcquisitionType.Census,
            SubType = SftpAcquisitionSubType.CernerCCLExtract,
            RemoteDirectory = "/data/census",
            FileNamePattern = "*.dat",
            ProcessedDirectory = null
        };
        var sftpConfig = new SftpConfigurationModel
        {
            OrganizationId = "TestFacility",
            RemoveAfterProcessing = true
        };

        await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, sftpConfig, configNoProcessedDir, CancellationToken.None);

        _sessionMock.Verify(s => s.DeleteFileAsync(
            "/data/census/test.dat",
            It.IsAny<CancellationToken>()), Times.Once);

        _sessionMock.Verify(s => s.MoveFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWithSessionAsync_NoProcessedDirAndNoRemove_FileLeftInPlace()
    {
        var files = new List<SftpFileInfo>
        {
            new() { Name = "test.dat", FullName = "/data/census/test.dat" }
        };
        var content = "12345|67890|Fac|U|1|A|F|M|N|Active|IP|20230101120000|";

        SetupSessionListFiles(files);
        SetupSessionDownload("/data/census/test.dat", CreateStream(content));
        SetupRealCernerParser();

        var configNoProcessedDir = new SftpAcquisitionTypeConfiguration
        {
            AcquisitionType = SftpAcquisitionType.Census,
            SubType = SftpAcquisitionSubType.CernerCCLExtract,
            RemoteDirectory = "/data/census",
            FileNamePattern = "*.dat",
            ProcessedDirectory = null
        };
        var sftpConfig = new SftpConfigurationModel
        {
            OrganizationId = "TestFacility",
            RemoveAfterProcessing = false
        };

        var result = await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, sftpConfig, configNoProcessedDir, CancellationToken.None);

        // File still considered processed (added to result list) even though left in place
        Assert.Single(result);

        _sessionMock.Verify(s => s.MoveFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionMock.Verify(s => s.DeleteFileAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWithSessionAsync_ProcessedDirectoryTakesPriorityOverRemoveFlag()
    {
        var files = new List<SftpFileInfo>
        {
            new() { Name = "test.dat", FullName = "/data/census/test.dat" }
        };
        var content = "12345|67890|Fac|U|1|A|F|M|N|Active|IP|20230101120000|";

        SetupSessionListFiles(files);
        SetupSessionDownload("/data/census/test.dat", CreateStream(content));
        SetupRealCernerParser();

        // Both ProcessedDirectory and RemoveAfterProcessing set - ProcessedDirectory should win
        var sftpConfig = new SftpConfigurationModel
        {
            OrganizationId = "TestFacility",
            RemoveAfterProcessing = true // <-- also true
        };

        await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, sftpConfig, _testAcquisitionConfig, CancellationToken.None);

        // Move should be used (ProcessedDirectory is set on _testAcquisitionConfig)
        _sessionMock.Verify(s => s.MoveFileAsync(
            "/data/census/test.dat", "/data/processed", It.IsAny<CancellationToken>()), Times.Once);

        // Delete should NOT be called
        _sessionMock.Verify(s => s.DeleteFileAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ProcessWithSessionAsync - Directory fallback

    [Fact]
    public async Task ProcessWithSessionAsync_NoAcquisitionRemoteDir_FallsBackToSftpConfigDir()
    {
        var files = new List<SftpFileInfo>();
        var configNoDir = new SftpAcquisitionTypeConfiguration
        {
            AcquisitionType = SftpAcquisitionType.Census,
            SubType = SftpAcquisitionSubType.CernerCCLExtract,
            RemoteDirectory = null,
            FileNamePattern = "*.dat"
        };

        _sessionMock.Setup(s => s.ListFilesAsync(
                "/data", "*.dat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, _testSftpConfig, configNoDir, CancellationToken.None);

        // Should use SftpConfigurationModel.RemoteDirectory as fallback
        _sessionMock.Verify(s => s.ListFilesAsync(
            "/data", "*.dat", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWithSessionAsync_NoBothDirs_FallsBackToRoot()
    {
        var files = new List<SftpFileInfo>();
        var configNoDir = new SftpAcquisitionTypeConfiguration
        {
            AcquisitionType = SftpAcquisitionType.Census,
            SubType = SftpAcquisitionSubType.CernerCCLExtract,
            RemoteDirectory = null,
            FileNamePattern = "*.dat"
        };
        var sftpConfigNoDir = new SftpConfigurationModel
        {
            OrganizationId = "TestFacility",
            RemoteDirectory = null
        };

        _sessionMock.Setup(s => s.ListFilesAsync(
                "/", "*.dat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, sftpConfigNoDir, configNoDir, CancellationToken.None);

        _sessionMock.Verify(s => s.ListFilesAsync(
            "/", "*.dat", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ProcessWithSessionAsync - Kafka event structure

    [Fact]
    public async Task ProcessWithSessionAsync_KafkaMessage_ContainsCorrelationHeader()
    {
        var files = new List<SftpFileInfo>
        {
            new() { Name = "test.dat", FullName = "/data/census/test.dat" }
        };
        var content = "12345|67890|Fac|U|1|A|F|M|N|Active|IP|20230101120000|";

        SetupSessionListFiles(files);
        SetupSessionDownload("/data/census/test.dat", CreateStream(content));
        SetupRealCernerParser();

        Message<string, CernerPatientsAcquired>? capturedMessage = null;
        _kafkaProducerMock.Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, CernerPatientsAcquired>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, CernerPatientsAcquired>, CancellationToken>(
                (_, msg, _) => capturedMessage = msg)
            .ReturnsAsync(new DeliveryResult<string, CernerPatientsAcquired>());

        await _processor.ProcessWithSessionAsync(
            _testLog, _sessionMock.Object, _testSftpConfig, _testAcquisitionConfig, CancellationToken.None);

        Assert.NotNull(capturedMessage);
        Assert.NotNull(capturedMessage.Headers);

        var correlationHeader = capturedMessage.Headers.FirstOrDefault(h => h.Key == "X-Correlation-Id");
        Assert.NotNull(correlationHeader);

        var correlationId = Encoding.UTF8.GetString(correlationHeader.GetValueBytes());
        Assert.True(Guid.TryParse(correlationId, out _), "X-Correlation-Id should be a valid GUID");
    }

    #endregion

    #region ProcessAsync - Session lifecycle

    [Fact]
    public async Task ProcessAsync_OpensSessionAndDelegates()
    {
        _sftpClientServiceMock.Setup(s => s.OpenSessionAsync(
                _testSftpConfig, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_sessionMock.Object);

        SetupSessionListFiles(new List<SftpFileInfo>());

        await _processor.ProcessAsync(
            _testLog, _testSftpConfig, _testAcquisitionConfig, CancellationToken.None);

        _sftpClientServiceMock.Verify(s => s.OpenSessionAsync(
            _testSftpConfig, It.IsAny<CancellationToken>()), Times.Once);

        // Session should be disposed via await using
        _sessionMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    #endregion

    #region Helpers

    private void SetupSessionListFiles(List<SftpFileInfo> files)
    {
        _sessionMock.Setup(s => s.ListFilesAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);
    }

    private void SetupSessionDownload(string path, MemoryStream stream)
    {
        _sessionMock.Setup(s => s.DownloadFileAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);
    }

    private void SetupRealCernerParser()
    {
        // Use the real parser to validate end-to-end data flow
        var realParser = new CernerCclExtractParser(
            new Mock<ILogger<CernerCclExtractParser>>().Object);

        _parserFactoryMock.Setup(f => f.GetParser<CernerEncounters>(
                It.IsAny<SftpAcquisitionType>(),
                It.IsAny<SftpAcquisitionSubType>(),
                It.IsAny<string>(),
                It.IsAny<FileParsingConfiguration?>()))
            .Returns(realParser);
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    #endregion
}
