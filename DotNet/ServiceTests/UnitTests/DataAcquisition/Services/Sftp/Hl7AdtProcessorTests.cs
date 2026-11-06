using System.Text;
using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Processors;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services.Sftp;

[Trait("Category", "UnitTests")]
public class Hl7AdtProcessorTests
{
    private const string FacilityId = "TestFacility";
    private const string MrnSystem = "urn:oid:1.2.3";

    private readonly Mock<ISftpSession> _session = new();
    private readonly Mock<IFileParserFactory> _parserFactory = new();
    private readonly Mock<IFhirQueryConfigurationQueries> _fhirQueryConfigurationQueries = new();
    private readonly Mock<IAdtPatientIdResolver> _resolver = new();
    private readonly Mock<IProducer<string, ADTAcquired>> _producer = new();
    private readonly Mock<ILogger<Hl7AdtProcessor>> _logger = new();
    private readonly List<ADTAcquired> _produced = [];
    private readonly SftpAcquisitionSettings _settings = new();
    private readonly SftpAcquisitionLog _log = new() { Id = 1, FacilityId = FacilityId, AcquisitionType = SftpAcquisitionType.Census, SubType = SftpAcquisitionSubType.HL7ADT };
    private readonly SftpConfigurationModel _sftpConfig = new() { OrganizationId = FacilityId, RemoteDirectory = "/data" };

    public Hl7AdtProcessorTests()
    {
        _parserFactory
            .Setup(f => f.GetParser<Hl7Message>(It.IsAny<SftpAcquisitionType>(), It.IsAny<SftpAcquisitionSubType>(), It.IsAny<string>(), It.IsAny<FileParsingConfiguration?>()))
            .Returns(new Hl7AdtFileParser());

        _producer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, ADTAcquired>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, ADTAcquired>, CancellationToken>((_, message, _) => _produced.Add(message.Value))
            .ReturnsAsync(new DeliveryResult<string, ADTAcquired>());

        _fhirQueryConfigurationQueries
            .Setup(q => q.GetByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FhirQueryConfigurationModel { FacilityId = FacilityId, FhirServerBaseUrl = "https://fhir.example.com" });
    }

    private Hl7AdtProcessor CreateProcessor() => new(
        _logger.Object,
        new Mock<ISftpClientService>().Object,
        _parserFactory.Object,
        _fhirQueryConfigurationQueries.Object,
        _resolver.Object,
        _producer.Object,
        Options.Create(_settings));

    private static SftpAcquisitionTypeConfiguration PatientIdOnEvent(string location = "PID-3[5=FHIR].1") => new()
    {
        AcquisitionType = SftpAcquisitionType.Census,
        SubType = SftpAcquisitionSubType.HL7ADT,
        ProcessedDirectory = "/processed",
        AdtConfiguration = new AdtConfiguration { PatientIdLocation = location }
    };

    private static SftpAcquisitionTypeConfiguration MrnSearch(string? mrnLocation = null) => new()
    {
        AcquisitionType = SftpAcquisitionType.Census,
        SubType = SftpAcquisitionSubType.HL7ADT,
        ProcessedDirectory = "/processed",
        AdtConfiguration = new AdtConfiguration { MrnIdentifierSystem = MrnSystem, MrnLocation = mrnLocation }
    };

    private static string Hl7(string type, string mrn, string fhirId = "fhir-1", string control = "C1") =>
        $"MSH|^~\\&|EHR|HOSP|LINK|NHSN|20260102103000||ADT^{type}|{control}|P|2.5\r" +
        $"PID|1||{mrn}^^^HOSP^MR~{fhirId}^^^LINK^FHIR\r" +
        "PV1|1|I|4E^401^A||||||||||||||||VISIT1\r";

    private void SetupFiles(params (string Name, string Content, DateTime Modified)[] files)
    {
        _session
            .Setup(s => s.ListFilesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(files.Select(f => new SftpFileInfo { Name = f.Name, FullName = "/data/" + f.Name, LastWriteTime = f.Modified }).ToList());

        foreach (var file in files)
        {
            _session
                .Setup(s => s.DownloadFileAsync("/data/" + file.Name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(file.Content)));
        }
    }

    private void SetupFile(string content) => SetupFiles(("adt.hl7", content, new DateTime(2026, 1, 2)));

    private void SetupSearch(string mrn, int matches, string? patientId = null) =>
        _resolver
            .Setup(r => r.SearchByMrnAsync(FacilityId, MrnSystem, mrn, It.IsAny<FhirQueryConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdtPatientSearchResult(matches, patientId));

    private Task<List<string>> Process(SftpAcquisitionTypeConfiguration config) =>
        CreateProcessor().ProcessWithSessionAsync(_log, _session.Object, _sftpConfig, config, CancellationToken.None);

    private void VerifyErrorLogged(string contains, Times times) =>
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(contains)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    [Fact]
    public void CanProcess_OnlyHl7AdtCensus()
    {
        var processor = CreateProcessor();

        Assert.True(processor.CanProcess(SftpAcquisitionType.Census, SftpAcquisitionSubType.HL7ADT));
        Assert.False(processor.CanProcess(SftpAcquisitionType.Census, SftpAcquisitionSubType.CernerCCLExtract));
        Assert.False(processor.CanProcess(SftpAcquisitionType.Resources, SftpAcquisitionSubType.HL7ADT));
    }

    [Fact]
    public async Task PatientIdOnEvent_ProducesOneEventPerFile_InFileOrder_AndMovesTheFile()
    {
        SetupFile(Hl7("A01", "M1", "fhir-1") + Hl7("A08", "M2", "fhir-2") + Hl7("A03", "M1", "Patient/fhir-1"));

        var files = await Process(PatientIdOnEvent());

        Assert.Equal(["adt.hl7"], files);
        var acquired = Assert.Single(_produced);
        Assert.Equal(["A01", "A08", "A03"], acquired.Events.Select(e => e.MessageType));
        Assert.Equal(["fhir-1", "fhir-2", "fhir-1"], acquired.Events.Select(e => e.FhirPatientId));
        Assert.Equal("M1", acquired.Events[0].MedicalRecordNumber);
        Assert.Equal("VISIT1", acquired.Events[0].EncounterId);
        _producer.Verify(p => p.ProduceAsync("ADTAcquired", It.Is<Message<string, ADTAcquired>>(m => m.Key == FacilityId), It.IsAny<CancellationToken>()), Times.Once);
        _session.Verify(s => s.MoveFileAsync("/data/adt.hl7", "/processed", It.IsAny<CancellationToken>()), Times.Once);
        _resolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PatientIdOnEvent_NotFound_LogsAndLeavesOutOnlyThatEvent()
    {
        SetupFile(Hl7("A01", "M1", "fhir-1", "C1") + "MSH|^~\\&|EHR||||||ADT^A01|C2\rPID|1||M2^^^HOSP^MR\r");

        await Process(PatientIdOnEvent());

        Assert.Equal(["fhir-1"], Assert.Single(_produced).Events.Select(e => e.FhirPatientId));
        VerifyErrorLogged("no Patient.Id was found", Times.Once());
        VerifyErrorLogged("C2", Times.Once());
    }

    [Fact]
    public async Task PatientIdOnEvent_LocationPointsAtSomethingElse_IsLeftOut()
    {
        SetupFile(Hl7("A01", "M1"));

        await Process(PatientIdOnEvent("PV1-3"));

        Assert.Empty(_produced);
        VerifyErrorLogged("not a valid FHIR id", Times.Once());
    }

    [Fact]
    public async Task MrnSearch_AttachesTheFoundPatient_AndSearchesOncePerMrn()
    {
        SetupFile(Hl7("A01", "M1") + Hl7("A02", "M1") + Hl7("A01", "M2"));
        SetupSearch("M1", 1, "fhir-1");
        SetupSearch("M2", 1, "fhir-2");

        await Process(MrnSearch());

        Assert.Equal(["fhir-1", "fhir-1", "fhir-2"], Assert.Single(_produced).Events.Select(e => e.FhirPatientId));
        _resolver.Verify(r => r.SearchByMrnAsync(FacilityId, MrnSystem, "M1", It.IsAny<FhirQueryConfigurationModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A40_WithSeveralMerges_IsOneEventPerMerge_EachWithItsOwnPatient()
    {
        SetupFile(
            "MSH|^~\\&|EHR|HOSP|LINK|NHSN|20260102103000||ADT^A40^ADT_A39|C1|P|2.5\rEVN|A40\r" +
            "PID|1||M2^^^HOSP^MR\rMRG|M1^^^HOSP^MR\r" +
            "PID|1||M4^^^HOSP^MR\rMRG|M3^^^HOSP^MR\r" +
            "PID|1||M6^^^HOSP^MR\rMRG|M5^^^HOSP^MR\r" +
            Hl7("A01", "M2", control: "C2"));
        SetupSearch("M2", 1, "fhir-2");
        SetupSearch("M4", 1, "fhir-4");
        SetupSearch("M6", 0);

        await Process(MrnSearch());

        // The merge whose surviving patient is not found is left out alone
        var events = Assert.Single(_produced).Events;
        Assert.Equal(["A40", "A40", "A01"], events.Select(e => e.MessageType));
        Assert.Equal(["fhir-2", "fhir-4", "fhir-2"], events.Select(e => e.FhirPatientId));
        Assert.Equal(["M1", "M3"], events.Take(2).Select(e => e.Merge!.PriorPatientIdentifierList.Single().Id));
        VerifyErrorLogged("returned no results", Times.Once());
    }

    [Theory]
    [InlineData(0, "returned no results")]
    [InlineData(2, "returned more than one result")]
    public async Task MrnSearch_NotExactlyOnePatient_LogsAndLeavesOut(int matches, string expectedLog)
    {
        SetupFile(Hl7("A01", "M1") + Hl7("A01", "M2"));
        SetupSearch("M1", matches);
        SetupSearch("M2", 1, "fhir-2");

        await Process(MrnSearch());

        Assert.Equal(["fhir-2"], Assert.Single(_produced).Events.Select(e => e.FhirPatientId));
        VerifyErrorLogged(expectedLog, Times.Once());
    }

    [Fact]
    public async Task MrnSearch_NoMrnOnEvent_LogsAndLeavesOut()
    {
        SetupFile("MSH|^~\\&|EHR||||||ADT^A01|C1\rPID|1||OTHER^^^HOSP^SS\r");

        await Process(MrnSearch());

        Assert.Empty(_produced);
        VerifyErrorLogged("no MRN was found", Times.Once());
        _resolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MrnSearch_NeverLogsTheMrn()
    {
        SetupFile(Hl7("A01", "SECRETMRN"));
        SetupSearch("SECRETMRN", 0);

        await Process(MrnSearch());

        _logger.Verify(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("SECRETMRN")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Fact]
    public async Task MrnLocation_Override_IsUsed()
    {
        SetupFile("MSH|^~\\&|EHR||||||ADT^A01|C1\rPID|1" + new string('|', 17) + "ACCT9\r");
        SetupSearch("ACCT9", 1, "fhir-9");

        await Process(MrnSearch("PID-18"));

        Assert.Equal("fhir-9", Assert.Single(Assert.Single(_produced).Events).FhirPatientId);
    }

    [Fact]
    public async Task MrnSearch_FhirServerFailure_ThrowsForRetry_WithoutProducingOrMoving()
    {
        SetupFile(Hl7("A01", "M1"));
        _resolver
            .Setup(r => r.SearchByMrnAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FhirQueryConfigurationModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unreachable"));

        await Assert.ThrowsAsync<HttpRequestException>(() => Process(MrnSearch()));

        Assert.Empty(_produced);
        _session.Verify(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MrnSearch_NoFhirQueryConfiguration_NeedsConfiguration()
    {
        SetupFile(Hl7("A01", "M1"));
        _fhirQueryConfigurationQueries
            .Setup(q => q.GetByFacilityIdAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirQueryConfigurationModel?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Process(MrnSearch()));
    }

    [Fact]
    public async Task NoAdtConfiguration_NeedsConfiguration()
    {
        var config = PatientIdOnEvent();
        config.AdtConfiguration = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Process(config));
    }

    [Fact]
    public async Task FilesAreReadOldestFirst_BreakingTiesByName()
    {
        var same = new DateTime(2026, 1, 2);
        SetupFiles(
            ("c.hl7", Hl7("A03", "M1", "fhir-c"), same),
            ("b.hl7", Hl7("A02", "M1", "fhir-b"), same),
            ("a.hl7", Hl7("A01", "M1", "fhir-a"), same.AddHours(-1)));

        var files = await Process(PatientIdOnEvent());

        Assert.Equal(["a.hl7", "b.hl7", "c.hl7"], files);
        Assert.Equal(["fhir-a", "fhir-b", "fhir-c"], _produced.Select(p => p.Events.Single().FhirPatientId));
    }

    [Fact]
    public async Task FileOverTheSizeLimit_IsSplitInOrder_AndNothingIsLost()
    {
        _settings.AdtMaxMessageBytes = 1_500;
        var content = string.Concat(Enumerable.Range(1, 30).Select(i => Hl7("A01", "M" + i, "fhir-" + i)));
        SetupFile(content);

        await Process(PatientIdOnEvent());

        Assert.True(_produced.Count > 1);
        Assert.Equal(
            Enumerable.Range(1, 30).Select(i => "fhir-" + i),
            _produced.SelectMany(p => p.Events).Select(e => e.FhirPatientId));
        Assert.All(_produced, p => Assert.True(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(p).Length <= 1_600));
        _session.Verify(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EveryEventLeftOut_ProducesNothing_ButTheFileIsStillHandled()
    {
        SetupFile("MSH|^~\\&|EHR||||||ADT^A01|C1\rPID|1\r");

        var files = await Process(PatientIdOnEvent());

        Assert.Equal(["adt.hl7"], files);
        Assert.Empty(_produced);
        _session.Verify(s => s.MoveFileAsync("/data/adt.hl7", "/processed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoProcessedDirectory_DeletesWhenConfiguredTo()
    {
        var config = PatientIdOnEvent();
        config.ProcessedDirectory = null;
        _sftpConfig.RemoveAfterProcessing = true;
        SetupFile(Hl7("A01", "M1"));

        await Process(config);

        _session.Verify(s => s.DeleteFileAsync("/data/adt.hl7", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resolver_SearchesByIdentifierSystemAndMrn_AndCountsOnlyPatients()
    {
        var search = new Mock<ISearchFhirCommand>();
        SearchFhirCommandRequest? request = null;
        search
            .Setup(s => s.ExecuteNonPagingAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SearchFhirCommandRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new Bundle
            {
                Entry =
                [
                    new Bundle.EntryComponent { Resource = new Patient { Id = "fhir-1" } },
                    new Bundle.EntryComponent { Resource = new OperationOutcome() }
                ]
            });

        var result = await new AdtPatientIdResolver(search.Object).SearchByMrnAsync(
            FacilityId, MrnSystem, "M|1", new FhirQueryConfigurationModel { FacilityId = FacilityId }, CancellationToken.None);

        Assert.Equal(1, result.MatchCount);
        Assert.Equal("fhir-1", result.PatientId);
        Assert.Equal(global::Hl7.Fhir.Model.ResourceType.Patient, request!.resourceType);
        Assert.Contains(request.searchParams.Parameters, p => p.Item1 == "identifier" && p.Item2 == @"urn:oid:1.2.3|M\|1");
    }

    [Fact]
    public async Task Resolver_NextPage_MeansMoreThanOnePatient()
    {
        var search = new Mock<ISearchFhirCommand>();
        var bundle = new Bundle { Entry = [new Bundle.EntryComponent { Resource = new Patient { Id = "fhir-1" } }] };
        bundle.Link.Add(new Bundle.LinkComponent { Relation = "next", Url = "https://fhir.example.com/next" });
        search.Setup(s => s.ExecuteNonPagingAsync(It.IsAny<SearchFhirCommandRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(bundle);

        var result = await new AdtPatientIdResolver(search.Object).SearchByMrnAsync(
            FacilityId, MrnSystem, "M1", new FhirQueryConfigurationModel { FacilityId = FacilityId }, CancellationToken.None);

        Assert.Equal(2, result.MatchCount);
        Assert.Null(result.PatientId);
    }
}

[Trait("Category", "UnitTests")]
public class AdtConfigurationValidationTests
{
    private readonly SftpConfigurationModelValidator _validator = new(Options.Create(new SftpValidationSettings()));

    private static SftpConfigurationModel Model(AdtConfiguration? adt, string? processedDirectory = "/processed", bool removeAfterProcessing = false, SftpAcquisitionSubType subType = SftpAcquisitionSubType.HL7ADT) => new()
    {
        Id = Guid.NewGuid(),
        Host = "sftp.example.com",
        Port = 22,
        RemoteDirectory = "/",
        Timeout = TimeSpan.FromMinutes(1),
        RemoveAfterProcessing = removeAfterProcessing,
        AcquisitionConfigurations =
        [
            new SftpAcquisitionTypeConfiguration
            {
                AcquisitionType = SftpAcquisitionType.Census,
                SubType = subType,
                ProcessedDirectory = processedDirectory,
                AdtConfiguration = adt
            }
        ]
    };

    [Theory]
    [InlineData("PID-3[5=FHIR].1", null)]
    [InlineData("OBX[3.1=FHIRID]-5", null)]
    [InlineData(null, "urn:oid:1.2.3")]
    [InlineData(null, "https://hospital.example.com/mrn")]
    public void ExactlyOneOption_IsValid(string? location, string? system)
    {
        Assert.True(_validator.Validate(Model(new AdtConfiguration { PatientIdLocation = location, MrnIdentifierSystem = system })).IsValid);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", "")]
    [InlineData("PID-2", "urn:oid:1.2.3")]
    public void NeitherOrBothOptions_IsInvalid(string? location, string? system)
    {
        Assert.False(_validator.Validate(Model(new AdtConfiguration { PatientIdLocation = location, MrnIdentifierSystem = system })).IsValid);
    }

    [Fact]
    public void MissingAdtConfiguration_IsInvalid()
    {
        Assert.False(_validator.Validate(Model(null)).IsValid);
    }

    [Fact]
    public void AdtConfigurationOnAnotherSubType_IsInvalid()
    {
        Assert.False(_validator.Validate(Model(new AdtConfiguration { PatientIdLocation = "PID-2" }, subType: SftpAcquisitionSubType.CernerCCLExtract)).IsValid);
    }

    [Theory]
    [InlineData("PatientIdLocation")]
    [InlineData("MrnLocation")]
    [InlineData("EncounterIdLocation")]
    public void InvalidLocation_IsInvalid(string property)
    {
        var adt = new AdtConfiguration { MrnIdentifierSystem = "urn:oid:1.2.3" };
        if (property == "PatientIdLocation")
        {
            adt = new AdtConfiguration { PatientIdLocation = "not a location" };
        }
        else
        {
            typeof(AdtConfiguration).GetProperty(property)!.SetValue(adt, "not a location");
        }

        Assert.False(_validator.Validate(Model(adt)).IsValid);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("a|b")]
    public void InvalidSystem_IsInvalid(string system)
    {
        Assert.False(_validator.Validate(Model(new AdtConfiguration { MrnIdentifierSystem = system })).IsValid);
    }

    [Fact]
    public void FilesThatStayInTheDirectory_IsInvalid()
    {
        var adt = new AdtConfiguration { PatientIdLocation = "PID-2" };

        Assert.False(_validator.Validate(Model(adt, processedDirectory: null)).IsValid);
        Assert.True(_validator.Validate(Model(adt, processedDirectory: null, removeAfterProcessing: true)).IsValid);
    }

    [Fact]
    public void CreateValidator_AppliesTheSameRules()
    {
        var validator = new CreateSftpConfigurationModelValidator(Options.Create(new SftpValidationSettings()));
        var model = new CreateSftpConfigurationModel
        {
            Host = "sftp.example.com",
            Port = 22,
            RemoteDirectory = "/",
            Timeout = TimeSpan.FromMinutes(1),
            AcquisitionConfigurations =
            [
                new SftpAcquisitionTypeConfiguration { AcquisitionType = SftpAcquisitionType.Census, SubType = SftpAcquisitionSubType.HL7ADT }
            ]
        };

        Assert.False(validator.Validate(model).IsValid);
    }
}
