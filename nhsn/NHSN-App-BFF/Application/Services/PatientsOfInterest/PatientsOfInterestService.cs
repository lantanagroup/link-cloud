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

        // With the username and password in hand, the ad-hoc test both checks the connection and
        // previews the files in the report directory. Saved credentials are write-only, so a retest
        // that leaves them blank can only check the saved configuration, which lists no files.
        ConnectionResult result;
        IReadOnlyList<SftpFile> files;
        if (!string.IsNullOrWhiteSpace(config.Username) && !string.IsNullOrWhiteSpace(config.Password))
        {
            (result, files) = await _sftpFileGateway.TestConnectionWithPreviewAsync(config, cancellationToken);
        }
        else
        {
            result = await _sftpFileGateway.TestConnectionAsync(facilityId, cancellationToken);
            files = [];
        }

        if (result.Success)
        {
            _cache.Set(SftpFilesCacheKey(facilityId), files, SftpFilesCacheDuration);
        }

        return result;
    }

    public Task<IReadOnlyList<SftpFile>> GetSftpFilesAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        var files = _cache.Get<IReadOnlyList<SftpFile>>(SftpFilesCacheKey(facilityId)) ?? [];

        return Task.FromResult(files);
    }

    public Task<CensusListResult> QueryPatientListAsync(string listKey, CancellationToken cancellationToken = default) =>
        _patientListGateway.QueryAsync(_userContext.RequireFacilityId(), listKey, cancellationToken);

    public Task<IReadOnlyList<CensusListResult>> QueryPatientListsAsync(CancellationToken cancellationToken = default) =>
        _patientListGateway.QueryAllAsync(_userContext.RequireFacilityId(), cancellationToken);

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
