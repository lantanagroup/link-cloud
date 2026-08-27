using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Acknowledgements;

public sealed class AcknowledgementService : IAcknowledgementService
{
    private readonly NhsnAppDbContext _dbContext;

    public AcknowledgementService(NhsnAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool?> GetLatestAsync(string facilityId, AcknowledgementKind kind, string? contextId = null, CancellationToken cancellationToken = default)
    {
        var latest = await _dbContext.Acknowledgements
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId && x.Kind == kind && x.ContextId == contextId)
            .OrderByDescending(x => x.AcceptedOn)
            .FirstOrDefaultAsync(cancellationToken);

        return latest?.Accepted;
    }

    public async Task RecordAsync(
        string facilityId,
        AcknowledgementKind kind,
        string? contextId,
        bool accepted,
        string statementKey,
        string statementVersion,
        string acceptedByExternalUserId,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Acknowledgements.Add(new Acknowledgement
        {
            FacilityId = facilityId,
            Kind = kind,
            ContextId = contextId,
            Accepted = accepted,
            StatementKey = statementKey,
            StatementVersion = statementVersion,
            AcceptedByExternalUserId = acceptedByExternalUserId
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
