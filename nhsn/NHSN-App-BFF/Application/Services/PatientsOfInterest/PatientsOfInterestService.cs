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
    private readonly IPatientListGateway _patientListGateway;
    private readonly IAcknowledgementService _acknowledgementService;
    private readonly INhsnUserContext _userContext;
    private readonly IMemoryCache _cache;

    public PatientsOfInterestService(
        ISftpFileGateway sftpFileGateway,
        IPatientListGateway patientListGateway,
        IAcknowledgementService acknowledgementService,
        INhsnUserContext userContext,
        IMemoryCache cache)
    {
        _sftpFileGateway = sftpFileGateway;
        _patientListGateway = patientListGateway;
        _acknowledgementService = acknowledgementService;
        _userContext = userContext;
        _cache = cache;
    }

    public async Task<ConnectionResult> TestSftpConnectionAsync(SftpConfig config, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        var files = await _sftpFileGateway.TestConnectionAsync(facilityId, config, cancellationToken);

        _cache.Set(SftpFilesCacheKey(facilityId), files, SftpFilesCacheDuration);

        return new ConnectionResult { Success = true, MessageKey = "census.sftp.simulated", Simulated = true };
    }

    public Task<IReadOnlyList<SftpFile>> GetSftpFilesAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        var files = _cache.Get<IReadOnlyList<SftpFile>>(SftpFilesCacheKey(facilityId)) ?? [];

        return Task.FromResult(files);
    }

    public Task<CensusListResult> QueryPatientListAsync(string listKey, CancellationToken cancellationToken = default) =>
        _patientListGateway.QueryAsync(_userContext.RequireFacilityId(), listKey, cancellationToken);

    public Task AcknowledgeCensusAsync(AcknowledgementRequest request, CancellationToken cancellationToken = default) =>
        _acknowledgementService.RecordAsync(
            _userContext.RequireFacilityId(),
            AcknowledgementKind.CensusAccuracy,
            contextId: null,
            request.Accepted,
            request.StatementKey,
            request.StatementVersion,
            _userContext.ExternalUserId,
            cancellationToken);

    private static string SftpFilesCacheKey(string facilityId) => $"sftp-files:{facilityId}";
}
