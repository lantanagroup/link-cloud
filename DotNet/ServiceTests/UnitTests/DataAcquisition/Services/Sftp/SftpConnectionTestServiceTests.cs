using System.Net.Sockets;
using System.Text;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using Microsoft.Extensions.Logging;
using Moq;
using Renci.SshNet.Common;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services.Sftp;

[Trait("Category", "UnitTests")]
public class SftpConnectionTestServiceTests
{
    private const string Host = "sftp.example.com";
    private const int Port = 2222;
    private const string Username = "facility-user";
    private const string Password = "Pa55-must-not-leak";
    private const string ReportDirectory = "/data";
    private const string RawExceptionDetail = "RAW-SSH-EXCEPTION-DETAIL";
    private const string SingleRow = "12345.00|67890.00|FacA|UnitB|101|A|FIN001|MRN001|Doe, John|Active|Inpatient|20230707130643|";

    private readonly Mock<ISftpClientService> _sftpClientServiceMock = new();
    private readonly Mock<ISftpSession> _sessionMock = new();
    private readonly CapturingLogger<SftpConnectionTestService> _logger = new();
    private readonly SftpConnectionTestService _service;

    public SftpConnectionTestServiceTests()
    {
        var parser = new CernerCclExtractParser(new Mock<ILogger<CernerCclExtractParser>>().Object);
        _service = new SftpConnectionTestService(_logger, _sftpClientServiceMock.Object, parser);

        _sftpClientServiceMock
            .Setup(s => s.OpenSessionAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<SftpCredentialsModel>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_sessionMock.Object);

        SetupListFiles();
    }

    #region Connecting

    [Fact]
    public async Task TestSftpConnectionAsync_OpensSessionWithRequestDetailsAndTestTimeout()
    {
        await TestAsync(includeFileContent: false);

        _sftpClientServiceMock.Verify(s => s.OpenSessionAsync(
            Host,
            Port,
            It.Is<SftpCredentialsModel>(c => c.Username == Username && c.Password == Password),
            SftpConnectionTestService.ConnectionTimeout,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ConnectionFailures))]
    public async Task TestSftpConnectionAsync_ConnectionFails_ReturnsFailureWithFixedMessage(
        Exception failure, string expectedMessageFragment)
    {
        SetupOpenSessionThrows(failure);

        var result = await TestAsync(includeFileContent: true);

        Assert.False(result.Success);
        Assert.Contains(expectedMessageFragment, result.Message);
        Assert.DoesNotContain(RawExceptionDetail, result.Message);
        Assert.Null(result.Files);
    }

    public static TheoryData<Exception, string> ConnectionFailures => new()
    {
        { new SshAuthenticationException(RawExceptionDetail), "Authentication failed" },
        { new SshOperationTimeoutException(RawExceptionDetail), "timed out" },
        { new SocketException((int)SocketError.HostNotFound), "could not be resolved" },
        { new SocketException((int)SocketError.TimedOut), "timed out" },
        { new SocketException((int)SocketError.ConnectionRefused), "Could not connect" },
        { new SshConnectionException(RawExceptionDetail), "Could not connect" },
        { new ProxyException(RawExceptionDetail), "Could not connect" },
        { new SshException(RawExceptionDetail), "rejected the connection" },
        { new ArgumentException(RawExceptionDetail), "details are invalid" }
    };

    [Fact]
    public async Task TestSftpConnectionAsync_CancelledButNotByCaller_ReportsTimedOut()
    {
        SetupOpenSessionThrows(new OperationCanceledException());

        var result = await TestAsync(includeFileContent: false);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Message);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_CallerCancels_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        SetupOpenSessionThrows(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => TestAsync(includeFileContent: false, cts.Token));
    }

    [Fact]
    public async Task TestSftpConnectionAsync_UnexpectedConnectionException_Propagates()
    {
        SetupOpenSessionThrows(new NullReferenceException());

        await Assert.ThrowsAsync<NullReferenceException>(() => TestAsync(includeFileContent: false));
    }

    #endregion

    #region Report directory

    [Fact]
    public async Task TestSftpConnectionAsync_IncludeFileContentFalse_ListsDirectoryAndReturnsNoFiles()
    {
        SetupListFiles(RemoteFile("census_1.dat"));

        var result = await TestAsync(includeFileContent: false);

        Assert.True(result.Success);
        Assert.Null(result.Files);
        Assert.Contains($"'{ReportDirectory}'", result.Message);
        _sessionMock.Verify(s => s.ListFilesAsync(ReportDirectory, null, It.IsAny<CancellationToken>()), Times.Once);
        _sessionMock.Verify(s => s.DownloadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_EmptyDirectory_ReturnsSuccessWithEmptyFiles()
    {
        SetupListFiles();

        var result = await TestAsync(includeFileContent: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Files);
        Assert.Empty(result.Files);
    }

    [Theory]
    [MemberData(nameof(DirectoryFailures))]
    public async Task TestSftpConnectionAsync_DirectoryCannotBeRead_ReturnsFailureWithFixedMessage(
        Exception failure, string expectedMessageFragment)
    {
        SetupListFilesThrows(failure);

        var result = await TestAsync(includeFileContent: true);

        Assert.False(result.Success);
        Assert.Contains(expectedMessageFragment, result.Message);
        Assert.DoesNotContain(RawExceptionDetail, result.Message);
        Assert.Null(result.Files);
    }

    public static TheoryData<Exception, string> DirectoryFailures => new()
    {
        // SftpSession.ListFilesAsync throws InvalidOperationException for a missing directory
        { new InvalidOperationException(RawExceptionDetail), "does not exist" },
        { new SftpPathNotFoundException(RawExceptionDetail), "does not exist" },
        { new SftpPermissionDeniedException(RawExceptionDetail), "does not have permission" },
        { new SshConnectionException(RawExceptionDetail), "Could not connect" }
    };

    [Fact]
    public async Task TestSftpConnectionAsync_ObjectDisposedWhileListing_PropagatesRatherThanReportingMissingDirectory()
    {
        SetupListFilesThrows(new ObjectDisposedException("session"));

        await Assert.ThrowsAsync<ObjectDisposedException>(() => TestAsync(includeFileContent: true));
    }

    [Fact]
    public async Task TestSftpConnectionAsync_Succeeds_DisposesSession()
    {
        await TestAsync(includeFileContent: true);

        _sessionMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_DirectoryFails_DisposesSession()
    {
        SetupListFilesThrows(new SftpPermissionDeniedException(RawExceptionDetail));

        await TestAsync(includeFileContent: true);

        _sessionMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    #endregion

    #region File preview

    [Fact]
    public async Task TestSftpConnectionAsync_CernerExtract_ReturnsEachPatientOnce()
    {
        SetupListFiles(RemoteFile("census_1.dat"));
        SetupDownload("census_1.dat", """
            person_id|encntr_id|facility|unit|room|bed|fin|mrn|pat_nam|enc_status|enc_type|admit_dt|disch_dt
            12345.00|67890.00|FacA|UnitB|101|A|FIN001|MRN001|Doe, John|Active|Inpatient|20230707130643|
            12345.00|67891.00|FacA|UnitB|101|A|FIN003|MRN001|Doe, John|Active|Inpatient|20230708130643|
            11111.00|22222.00|FacA|UnitC|102|B|FIN002|MRN002|Smith, Jane|Discharged|Emergency|20230801093015|20230801170000
            """);

        var result = await TestAsync(includeFileContent: true);

        Assert.True(result.Success);
        var file = Assert.Single(result.Files!);
        Assert.Equal("census_1.dat", file.FileName);
        Assert.Collection(file.Patients,
            patient =>
            {
                Assert.Equal("12345", patient.PatientId);
                Assert.Equal("Doe, John", patient.PatientName);
            },
            patient =>
            {
                Assert.Equal("11111", patient.PatientId);
                Assert.Equal("Smith, Jane", patient.PatientName);
            });
        Assert.Contains("Found 1 file(s).", result.Message);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_FileIsNotCernerExtract_ListedWithoutPatientsOrDownload()
    {
        SetupListFiles(RemoteFile("census_1.csv"));

        var result = await TestAsync(includeFileContent: true);

        var file = Assert.Single(result.Files!);
        Assert.Equal("census_1.csv", file.FileName);
        Assert.Empty(file.Patients);
        Assert.Contains("1 file(s) are not Cerner extracts", result.Message);
        _sessionMock.Verify(s => s.DownloadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_FileOverSizeLimit_ListedWithoutDownloading()
    {
        SetupListFiles(RemoteFile("census_1.dat", length: SftpConnectionTestService.MaxPreviewFileSizeBytes + 1));

        var result = await TestAsync(includeFileContent: true);

        var file = Assert.Single(result.Files!);
        Assert.Empty(file.Patients);
        Assert.Contains("1 file(s) larger than 10 MB were not previewed.", result.Message);
        _sessionMock.Verify(s => s.DownloadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_MoreFilesThanLimit_ReturnsMostRecentlyModified()
    {
        var max = SftpConnectionTestService.MaxFilesReturned;
        var oldest = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        SetupListFiles(Enumerable.Range(0, max + 5)
            .Select(i => RemoteFile($"notes_{i}.txt", lastWriteTime: oldest.AddMinutes(i)))
            .ToArray());

        var result = await TestAsync(includeFileContent: true);

        Assert.Equal(max, result.Files!.Length);
        Assert.Equal($"notes_{max + 4}.txt", result.Files[0].FileName);
        Assert.Contains(result.Files, f => f.FileName == "notes_5.txt");
        Assert.DoesNotContain(result.Files, f => f.FileName == "notes_4.txt");
        Assert.Contains($"Found {max + 5} file(s).", result.Message);
        Assert.Contains($"Only the {max} most recently modified are listed.", result.Message);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_FileCannotBeRead_SkipsItAndPreviewsTheRest()
    {
        SetupListFiles(RemoteFile("census_1.dat"), RemoteFile("census_2.dat"));
        SetupDownload("census_1.dat", SingleRow);
        _sessionMock
            .Setup(s => s.DownloadFileAsync($"{ReportDirectory}/census_2.dat", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SftpPermissionDeniedException(RawExceptionDetail));

        var result = await TestAsync(includeFileContent: true);

        Assert.True(result.Success);
        Assert.Equal(2, result.Files!.Length);
        Assert.Single(result.Files.Single(f => f.FileName == "census_1.dat").Patients);
        Assert.Empty(result.Files.Single(f => f.FileName == "census_2.dat").Patients);
        Assert.Contains("1 file(s) could not be read.", result.Message);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_PreviewingFiles_NeverMovesOrDeletesAnything()
    {
        SetupListFiles(RemoteFile("census_1.dat"), RemoteFile("other.csv"));
        SetupDownload("census_1.dat", SingleRow);

        await TestAsync(includeFileContent: true);

        _sessionMock.Verify(s => s.MoveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionMock.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    [Theory]
    [MemberData(nameof(UnrelatedPreviewFailures))]
    public async Task TestSftpConnectionAsync_UnrelatedExceptionWhilePreviewing_PropagatesRatherThanBeingReportedAsADirectoryOrDetailsProblem(
        Exception failure)
    {
        SetupListFiles(RemoteFile("census_1.dat"));
        _sessionMock
            .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => TestAsync(includeFileContent: true));

        Assert.Same(failure, thrown);
    }

    public static TheoryData<Exception> UnrelatedPreviewFailures => new()
    {
        // Only the ListFilesAsync call means a missing directory
        new InvalidOperationException(RawExceptionDetail),
        // Only opening the session means invalid connection details
        new ArgumentException(RawExceptionDetail)
    };

    #region Preview limits

    [Fact]
    public async Task TestSftpConnectionAsync_FileAtThePerFileLimit_IsPreviewedInFull()
    {
        var max = SftpConnectionTestService.MaxPatientsPerFile;
        SetupListFiles(RemoteFile("census_1.dat"));
        SetupDownload("census_1.dat", ExtractWithPatients(max));

        var result = await TestAsync(includeFileContent: true);

        Assert.Equal(max, Assert.Single(result.Files!).Patients.Length);
        Assert.DoesNotContain("partly previewed", result.Message);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_FileOverThePerFileLimit_IsPreviewedInPart()
    {
        var max = SftpConnectionTestService.MaxPatientsPerFile;
        SetupListFiles(RemoteFile("census_1.dat"));
        SetupDownload("census_1.dat", ExtractWithPatients(max + 1));

        var result = await TestAsync(includeFileContent: true);

        Assert.True(result.Success);
        Assert.Equal(max, Assert.Single(result.Files!).Patients.Length);
        Assert.Contains("1 file(s) were only partly previewed", result.Message);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_TotalLimitReached_RemainingExtractsAreListedButNotRead()
    {
        var perFile = SftpConnectionTestService.MaxPatientsPerFile;
        var total = SftpConnectionTestService.MaxPatientsTotal;
        var newest = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var names = Enumerable.Range(1, total / perFile + 1).Select(i => $"census_{i}.dat").ToArray();
        SetupListFiles(names.Select((name, i) => RemoteFile(name, lastWriteTime: newest.AddMinutes(-i))).ToArray());
        foreach (var name in names)
        {
            SetupDownload(name, ExtractWithPatients(perFile));
        }

        var result = await TestAsync(includeFileContent: true);

        Assert.True(result.Success);
        Assert.Equal(names.Length, result.Files!.Length);
        Assert.Equal(total, result.Files.Sum(f => f.Patients.Length));
        Assert.Empty(result.Files[^1].Patients);
        Assert.Contains($"1 file(s) were not previewed because the {total}-patient preview limit was reached.", result.Message);
        var lastFile = $"{ReportDirectory}/{names[^1]}";
        _sessionMock.Verify(s => s.DownloadFileAsync(lastFile, It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Nothing stored

    [Fact]
    public void Constructor_TakesNoConfigurationCredentialOrSecretDependency()
    {
        // Nothing passed to a connection test may be stored. A new dependency here must be
        // reviewed against that before it is added to this list.
        var allowed = new[]
        {
            typeof(ILogger<SftpConnectionTestService>),
            typeof(ISftpClientService),
            typeof(CernerCclExtractParser)
        };

        var parameterTypes = typeof(SftpConnectionTestService)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType);

        Assert.All(parameterTypes, type => Assert.Contains(type, allowed));
    }

    [Fact]
    public void RequestModel_ToString_OmitsThePassword()
    {
        var request = new SftpTestConnectionRequestModel
        {
            HostName = Host,
            HostUrlPort = Port,
            Username = Username,
            Password = Password,
            ReportDirectory = ReportDirectory
        };

        var text = request.ToString();

        Assert.DoesNotContain(Password, text);
        Assert.Contains($"HostName = {Host}", text);
        Assert.Contains($"ReportDirectory = {ReportDirectory}", text);
    }

    [Fact]
    public async Task TestSftpConnectionAsync_NeverLogsOrReturnsThePassword()
    {
        SetupListFiles(RemoteFile("census_1.dat"));
        SetupDownload("census_1.dat", SingleRow);
        var success = await TestAsync(includeFileContent: true);

        SetupOpenSessionThrows(new SshAuthenticationException(RawExceptionDetail));
        var failure = await TestAsync(includeFileContent: true);

        Assert.DoesNotContain(Password, success.Message);
        Assert.DoesNotContain(Password, failure.Message);
        Assert.NotEmpty(_logger.Entries);
        Assert.All(_logger.Entries, entry => Assert.DoesNotContain(Password, entry));
    }

    #endregion

    #region Helpers

    private Task<SftpTestConnectionResult> TestAsync(bool includeFileContent, CancellationToken cancellationToken = default)
        => _service.TestSftpConnectionAsync(
            Host, Port, Username, Password, ReportDirectory, includeFileContent, cancellationToken);

    private void SetupOpenSessionThrows(Exception exception)
        => _sftpClientServiceMock
            .Setup(s => s.OpenSessionAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<SftpCredentialsModel>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

    private void SetupListFiles(params SftpFileInfo[] files)
        => _sessionMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => files.ToList());

    private void SetupListFilesThrows(Exception exception)
        => _sessionMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

    private void SetupDownload(string fileName, string content)
        => _sessionMock
            .Setup(s => s.DownloadFileAsync($"{ReportDirectory}/{fileName}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));

    private static SftpFileInfo RemoteFile(string name, long length = 100, DateTime? lastWriteTime = null) => new()
    {
        Name = name,
        FullName = $"{ReportDirectory}/{name}",
        Length = length,
        LastWriteTime = lastWriteTime ?? new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static string ExtractWithPatients(int count)
    {
        var builder = new StringBuilder();
        for (var i = 1; i <= count; i++)
        {
            builder.Append($"{i}.00|{i}.00|Fac|Unit|101|A|FIN|MRN|Patient, Number {i}|Active|IP|20230101120000|\n");
        }

        return builder.ToString();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add($"{formatter(state, exception)} {exception}");
        }
    }

    #endregion
}
