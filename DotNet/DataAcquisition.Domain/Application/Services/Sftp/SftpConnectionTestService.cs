using System.Net.Sockets;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;
using Renci.SshNet.Common;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp;

/// <summary>
/// Tests SFTP connection details supplied directly by a caller, without a saved configuration.
/// Nothing passed to a test is stored.
/// </summary>
public interface ISftpConnectionTestService
{
    /// <summary>
    /// Connects to an SFTP server, verifies the report directory can be read and, optionally,
    /// previews the patients in each Cerner extract found there.
    /// </summary>
    /// <param name="hostName">Host name or IP address of the SFTP server.</param>
    /// <param name="hostUrlPort">Port of the SFTP server.</param>
    /// <param name="username">Username for SFTP authentication.</param>
    /// <param name="password">Password for SFTP authentication.</param>
    /// <param name="reportDirectory">Directory on the SFTP server to read.</param>
    /// <param name="includeFileContent">
    /// When true, the result lists the files in <paramref name="reportDirectory"/> with a preview of the patients in each.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The test result. A server that can't be reached, rejected credentials or an unreadable directory
    /// is reported as <see cref="SftpTestConnectionResult.Success"/> false with a reason, not thrown.
    /// </returns>
    Task<SftpTestConnectionResult> TestSftpConnectionAsync(
        string hostName,
        int hostUrlPort,
        string username,
        string password,
        string reportDirectory,
        bool includeFileContent,
        CancellationToken cancellationToken);
}

/// <summary>
/// Tests ad-hoc SFTP connection details. Only reads from the server: files are listed and downloaded, never moved or deleted.
/// </summary>
/// <remarks>
/// Deliberately takes no configuration, credential or secret dependency, so nothing passed to a test can be persisted.
/// </remarks>
public class SftpConnectionTestService(
    ILogger<SftpConnectionTestService> logger,
    ISftpClientService sftpClientService,
    CernerCclExtractParser cernerParser) : ISftpConnectionTestService
{
    /// <summary>
    /// Connection and operation timeout for a test. Shorter than a saved configuration's default because a caller is waiting.
    /// </summary>
    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Most files returned when previewing; the most recently modified are kept.
    /// </summary>
    public const int MaxFilesReturned = 50;

    /// <summary>
    /// Files larger than this are listed but not downloaded for a preview.
    /// </summary>
    public const long MaxPreviewFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Most patients previewed from a single file. A file with more is previewed in part.
    /// </summary>
    public const int MaxPatientsPerFile = 2_000;

    /// <summary>
    /// Most patients previewed across all files. Once reached, the remaining extracts are listed but not previewed.
    /// </summary>
    public const int MaxPatientsTotal = 10_000;

    private const string AuthenticationFailedMessage = "Authentication failed. Verify the username and password.";
    private const string TimedOutMessage = "The connection to the SFTP server timed out. Verify the host name and port, and that the server accepts connections from Link.";
    private const string HostNotFoundMessage = "The SFTP host name could not be resolved. Verify the host name.";
    private const string UnreachableMessage = "Could not connect to the SFTP server. Verify the host name and port, and that the server accepts connections from Link.";
    private const string ServerErrorMessage = "The SFTP server rejected the connection or closed it unexpectedly.";
    private const string InvalidDetailsMessage = "The SFTP connection details are invalid. Verify the host name and port.";

    /// <inheritdoc/>
    public async Task<SftpTestConnectionResult> TestSftpConnectionAsync(
        string hostName,
        int hostUrlPort,
        string username,
        string password,
        string reportDirectory,
        bool includeFileContent,
        CancellationToken cancellationToken)
    {
        ISftpSession session;

        try
        {
            session = await sftpClientService.OpenSessionAsync(
                hostName,
                hostUrlPort,
                new SftpCredentialsModel { Username = username, Password = password },
                ConnectionTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (!IsCallerCancellation(ex, cancellationToken) && DescribeConnectionFailure(ex) is { } reason)
        {
            return Failed(ex, reason, hostName, hostUrlPort);
        }

        await using (session)
        {
            try
            {
                var result = await ReadReportDirectoryAsync(session, reportDirectory, includeFileContent, cancellationToken);

                logger.LogInformation(
                    "SFTP connection test to {Host}:{Port} succeeded",
                    hostName.SanitizeForLog(), hostUrlPort.SanitizeForLog());

                return result;
            }
            catch (Exception ex) when (!IsCallerCancellation(ex, cancellationToken) && DescribeDirectoryFailure(ex, reportDirectory) is { } reason)
            {
                return Failed(ex, reason, hostName, hostUrlPort);
            }
        }
    }

    private async Task<SftpTestConnectionResult> ReadReportDirectoryAsync(
        ISftpSession session,
        string reportDirectory,
        bool includeFileContent,
        CancellationToken cancellationToken)
    {
        // Listing proves the directory is readable, which an existence check alone would not
        List<SftpFileInfo> files;
        try
        {
            files = await session.ListFilesAsync(reportDirectory, null, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex is not ObjectDisposedException)
        {
            // ListFilesAsync reports a missing directory as InvalidOperationException, which census acquisition
            // relies on. Narrow it here, at the one call where it means that, so an InvalidOperationException from
            // anywhere else in the test is not reported as a missing directory.
            throw new SftpPathNotFoundException(ex.Message, ex);
        }

        var message = $"Connected to the SFTP server and read the report directory '{reportDirectory}'.";

        if (!includeFileContent)
        {
            return new SftpTestConnectionResult { Success = true, Message = message };
        }

        var newestFiles = files
            .OrderByDescending(f => f.LastWriteTime)
            .Take(MaxFilesReturned)
            .ToList();

        var fileModels = new List<SftpTestFileModel>(newestFiles.Count);
        var notExtractCount = 0;
        var tooLargeCount = 0;
        var unreadableCount = 0;
        var partlyPreviewedCount = 0;
        var overPatientLimitCount = 0;
        var patientsPreviewed = 0;

        foreach (var file in newestFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileModel = new SftpTestFileModel { FileName = file.Name };
            fileModels.Add(fileModel);

            if (!IsCernerExtract(file.Name))
            {
                notExtractCount++;
                continue;
            }

            if (file.Length > MaxPreviewFileSizeBytes)
            {
                tooLargeCount++;
                continue;
            }

            var patientLimit = Math.Min(MaxPatientsPerFile, MaxPatientsTotal - patientsPreviewed);
            if (patientLimit == 0)
            {
                overPatientLimitCount++;
                continue;
            }

            try
            {
                var (patients, truncated) = await PreviewPatientsAsync(session, file, patientLimit, cancellationToken);
                fileModel.Patients = patients;
                patientsPreviewed += patients.Length;

                if (truncated)
                {
                    partlyPreviewedCount++;
                }
            }
            catch (Exception ex) when (ex is SftpPermissionDeniedException or SftpPathNotFoundException)
            {
                // Unreadable, or removed since the listing; the rest of the directory is still worth previewing
                logger.LogDebug(ex, "Could not read {FileName} during an SFTP connection test", file.Name.SanitizeForLog());
                unreadableCount++;
            }
        }

        message += $" Found {files.Count} file(s).";

        if (files.Count > MaxFilesReturned)
        {
            message += $" Only the {MaxFilesReturned} most recently modified are listed.";
        }

        if (notExtractCount > 0)
        {
            message += $" {notExtractCount} file(s) are not Cerner extracts and were not previewed.";
        }

        if (tooLargeCount > 0)
        {
            message += $" {tooLargeCount} file(s) larger than {MaxPreviewFileSizeBytes / (1024 * 1024)} MB were not previewed.";
        }

        if (unreadableCount > 0)
        {
            message += $" {unreadableCount} file(s) could not be read.";
        }

        if (partlyPreviewedCount > 0)
        {
            message += $" {partlyPreviewedCount} file(s) were only partly previewed; a preview shows at most {MaxPatientsPerFile} patients per file and {MaxPatientsTotal} in total.";
        }

        if (overPatientLimitCount > 0)
        {
            message += $" {overPatientLimitCount} file(s) were not previewed because the {MaxPatientsTotal}-patient preview limit was reached.";
        }

        return new SftpTestConnectionResult
        {
            Success = true,
            Message = message,
            Files = fileModels.ToArray()
        };
    }

    /// <summary>
    /// Reads at most <paramref name="limit"/> patients from an extract. Stops parsing as soon as one more
    /// patient is found, and reports that the preview was truncated.
    /// </summary>
    private async Task<(SftpTestFilePatientModel[] Patients, bool Truncated)> PreviewPatientsAsync(
        ISftpSession session,
        SftpFileInfo file,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var stream = await session.DownloadFileAsync(file.FullName, cancellationToken);

        var patients = new List<SftpTestFilePatientModel>();

        await foreach (var patient in cernerParser.Preview(stream, null, cancellationToken))
        {
            if (patients.Count == limit)
            {
                return (patients.ToArray(), true);
            }

            patients.Add(patient);
        }

        return (patients.ToArray(), false);
    }

    private bool IsCernerExtract(string fileName)
        => cernerParser.CanParse(
            SftpAcquisitionType.Census,
            SftpAcquisitionSubType.CernerCCLExtract,
            Path.GetExtension(fileName),
            null);

    private SftpTestConnectionResult Failed(Exception ex, string reason, string hostName, int hostUrlPort)
    {
        logger.LogWarning(
            ex,
            "SFTP connection test to {Host}:{Port} failed: {Reason}",
            hostName.SanitizeForLog(), hostUrlPort.SanitizeForLog(), reason.SanitizeForLog());

        return new SftpTestConnectionResult { Success = false, Message = reason };
    }

    private static bool IsCallerCancellation(Exception ex, CancellationToken cancellationToken)
        => ex is OperationCanceledException && cancellationToken.IsCancellationRequested;

    /// <summary>
    /// Maps a failure to reach or log in to the server to a message that is safe to return to the caller.
    /// Returns null for an unexpected exception, which is left to propagate.
    /// </summary>
    private static string? DescribeConnectionFailure(Exception ex) => ex switch
    {
        SshAuthenticationException => AuthenticationFailedMessage,
        SshOperationTimeoutException or TimeoutException or OperationCanceledException => TimedOutMessage,
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain } => HostNotFoundMessage,
        SocketException { SocketErrorCode: SocketError.TimedOut } => TimedOutMessage,
        SocketException or SshConnectionException or ProxyException => UnreachableMessage,
        SshException => ServerErrorMessage,
        ArgumentException => InvalidDetailsMessage,
        _ => null
    };

    /// <summary>
    /// Maps a failure to read the report directory to a message that is safe to return to the caller.
    /// Returns null for an unexpected exception, which is left to propagate.
    /// </summary>
    private static string? DescribeDirectoryFailure(Exception ex, string reportDirectory) => ex switch
    {
        // ReadReportDirectoryAsync turns ListFilesAsync's missing-directory InvalidOperationException into this
        SftpPathNotFoundException =>
            $"Connected to the SFTP server, but the report directory '{reportDirectory}' does not exist.",
        // Once connected, an ArgumentException is a bug here, not invalid connection details
        ArgumentException => null,
        SftpPermissionDeniedException =>
            $"Connected to the SFTP server, but the user does not have permission to read the report directory '{reportDirectory}'.",
        _ => DescribeConnectionFailure(ex)
    };
}
