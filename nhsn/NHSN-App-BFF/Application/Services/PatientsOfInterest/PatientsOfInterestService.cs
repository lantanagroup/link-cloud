using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.PatientsOfInterest;

public sealed class PatientsOfInterestService : IPatientsOfInterestService
{
    private readonly ISftpFileGateway _sftpFileGateway;
    private readonly INhsnUserContext _userContext;

    public PatientsOfInterestService(ISftpFileGateway sftpFileGateway, INhsnUserContext userContext)
    {
        _sftpFileGateway = sftpFileGateway;
        _userContext = userContext;
    }

    public Task<IReadOnlyList<SftpFile>> GetSftpFilesAsync(CancellationToken cancellationToken = default) =>
        _sftpFileGateway.ListFilesAsync(_userContext.RequireFacilityId(), cancellationToken);
}
