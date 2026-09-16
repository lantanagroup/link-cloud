using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.PatientsOfInterest;

public sealed class PatientsOfInterestService : IPatientsOfInterestService
{
    private static readonly TimeSpan SftpFilesCacheDuration = TimeSpan.FromMinutes(30);

    private readonly ISftpFileGateway _sftpFileGateway;
    private readonly ISftpConfigurationGateway _sftpConfigurationGateway;
    private readonly IPatientListGateway _patientListGateway;
    private readonly IAcknowledgementService _acknowledgementService;
    private readonly INhsnUserContext _userContext;
    private readonly IMemoryCache _cache;

    public PatientsOfInterestService(
        ISftpFileGateway sftpFileGateway,
        ISftpConfigurationGateway sftpConfigurationGateway,
        IPatientListGateway patientListGateway,
        IAcknowledgementService acknowledgementService,
        INhsnUserContext userContext,
        IMemoryCache cache)
    {
        _sftpFileGateway = sftpFileGateway;
        _sftpConfigurationGateway = sftpConfigurationGateway;
        _patientListGateway = patientListGateway;
        _acknowledgementService = acknowledgementService;
        _userContext = userContext;
        _cache = cache;
    }

    // Data Acquisition's connection test runs against the facility's saved configuration and
    // credentials, not whatever is currently typed into the form — so both are saved here first.
    // Order matters: Data Acquisition's credentials endpoint 404s unless the configuration already
    // exists, so the configuration must be saved before any credentials in it are.
    public async Task<ConnectionResult> TestSftpConnectionAsync(SftpConfig config, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        await _patientListGateway.DeleteConfigurationIfExistsAsync(facilityId, cancellationToken);
        await _sftpConfigurationGateway.SaveConfigurationAsync(facilityId, config, cancellationToken);

        if (!string.IsNullOrWhiteSpace(config.Username) && !string.IsNullOrWhiteSpace(config.Password))
        {
            await _sftpConfigurationGateway.SaveCredentialsAsync(facilityId, config.Username, config.Password, cancellationToken);
        }

        var result = await _sftpFileGateway.TestConnectionAsync(facilityId, cancellationToken);

        // The connection test itself is real; the file/patient preview is not — Data Acquisition
        // has no live directory-listing endpoint, only its internal scheduled acquisition job. Only
        // populate the (simulated) preview once a real connection has actually succeeded.
        if (result.Success)
        {
            _cache.Set(SftpFilesCacheKey(facilityId), GenerateSimulatedFiles(), SftpFilesCacheDuration);
        }

        return result;
    }

    private static IReadOnlyList<SftpFile> GenerateSimulatedFiles()
    {
        var queriedAt = DateTimeOffset.UtcNow;
        var random = Random.Shared;
        var patientSeq = 0;
        var files = new List<SftpFile>();

        foreach (var fileIndex in Enumerable.Range(1, random.Next(2, 6)))
        {
            var patientCount = random.Next(3, 16);
            var patientIds = new List<string>(patientCount);
            for (var i = 0; i < patientCount; i++)
            {
                patientSeq++;
                patientIds.Add($"SIMULATED-PATIENT-{patientSeq:D4}");
            }

            files.Add(new SftpFile
            {
                FileName = $"census_extract_{fileIndex}_{queriedAt:yyyy-MM-dd}.csv",
                QueriedAt = queriedAt,
                Simulated = true,
                PatientIds = patientIds
            });
        }

        return files;
    }

    public Task<IReadOnlyList<SftpFile>> GetSftpFilesAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        var files = _cache.Get<IReadOnlyList<SftpFile>>(SftpFilesCacheKey(facilityId)) ?? [];

        return Task.FromResult(files);
    }

    public Task<CensusListResult> QueryPatientListAsync(string listKey, CancellationToken cancellationToken = default) =>
        _patientListGateway.QueryAsync(_userContext.RequireFacilityId(), listKey, cancellationToken);

    public Task SaveSftpCredentialsAsync(SftpCredentialsRequest request, CancellationToken cancellationToken = default) =>
        _sftpConfigurationGateway.SaveCredentialsAsync(
            _userContext.RequireFacilityId(), request.Username, request.Password, cancellationToken);

    public Task AcknowledgeCensusAsync(AcknowledgementRequest request, CancellationToken cancellationToken = default) =>
        _acknowledgementService.RecordAsync(
            _userContext.RequireFacilityId(),
            AcknowledgementKind.CensusAccuracy,
            contextId: null,
            request.Accepted,
            request.StatementKey,
            _userContext.ExternalUserId,
            cancellationToken);

    private static string SftpFilesCacheKey(string facilityId) => $"sftp-files:{facilityId}";
}
