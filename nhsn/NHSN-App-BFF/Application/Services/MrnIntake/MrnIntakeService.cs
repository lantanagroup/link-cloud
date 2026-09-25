using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.MrnIntake;

public sealed class MrnIntakeService : IMrnIntakeService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly IPatientIdentifierGateway _patientIdentifierGateway;
    private readonly ILogger<MrnIntakeService> _logger;

    public MrnIntakeService(
        NhsnAppDbContext dbContext,
        INhsnUserContext userContext,
        IPatientIdentifierGateway patientIdentifierGateway,
        ILogger<MrnIntakeService> logger)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _patientIdentifierGateway = patientIdentifierGateway;
        _logger = logger;
    }

    public async Task<MrnIntakeResponse?> GetAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var row = await _dbContext.MrnIntakeRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (row is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MrnIntakeResponse>(row.IntakeJson, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "MRN intake for facility {FacilityId} could not be parsed; treating it as unsaved.", facilityId);
            return null;
        }
    }

    public async Task SaveAsync(MrnIntakeResponse intake, CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var row = await _dbContext.MrnIntakeRecords.SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);
        if (row is null)
        {
            row = new MrnIntakeRecord { FacilityId = facilityId };
            _dbContext.MrnIntakeRecords.Add(row);
        }

        row.IntakeJson = JsonSerializer.Serialize(intake);
        row.UpdatedOn = DateTime.UtcNow;
        row.UpdatedBy = _userContext.ExternalUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<PatientIdentifierResponse>> GetPatientIdentifiersAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();
        return _patientIdentifierGateway.GetForFacilityAsync(facilityId, cancellationToken);
    }

    public async Task<MrnIntakeOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.MrnIntakeOptionSets
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return new MrnIntakeOptionsResponse
        {
            MultipleMrnTypes = ToOptions(rows, "MultipleMrnType"),
            VarianceTypes = ToOptions(rows, "MrnVarianceType"),
            ChangeTypes = ToOptions(rows, "MrnChangeType")
        };
    }

    private static IReadOnlyList<MrnIntakeOptionResponse> ToOptions(IEnumerable<MrnIntakeOptionSet> rows, string optionGroup) =>
        rows
            .Where(x => x.OptionGroup == optionGroup)
            .Select(x => new MrnIntakeOptionResponse { Value = x.Value, LabelKey = x.LabelKey })
            .ToList();
}
