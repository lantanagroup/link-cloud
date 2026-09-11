using System.Text.Json;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

public sealed class OnboardingDraftStore : IOnboardingDraftStore
{
    private static readonly JsonSerializerOptions StepIdOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly NhsnAppDbContext _dbContext;
    private readonly INhsnUserContext _userContext;
    private readonly ILogger<OnboardingDraftStore> _logger;

    public OnboardingDraftStore(NhsnAppDbContext dbContext, INhsnUserContext userContext, ILogger<OnboardingDraftStore> logger)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<StoredDraft> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.OnboardingDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (row is null)
        {
            return new StoredDraft { SchemaVersion = DraftSchema.CurrentVersion };
        }

        if (row.SchemaVersion != DraftSchema.CurrentVersion)
        {
            _logger.LogInformation(
                "Draft for facility {FacilityId} stored at schema version {StoredVersion}; migrating on read to {CurrentVersion}.",
                facilityId, row.SchemaVersion, DraftSchema.CurrentVersion);
        }

        return new StoredDraft
        {
            State = DraftSchema.Read(row.DraftJson, row.SchemaVersion),
            UnlockedStepIds = ReadUnlockedSteps(row.UnlockedStepsJson, facilityId),
            SchemaVersion = row.SchemaVersion
        };
    }

    public async Task SaveAsync(string facilityId, StoredDraft draft, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.OnboardingDrafts
            .SingleOrDefaultAsync(x => x.FacilityId == facilityId, cancellationToken);

        if (row is null)
        {
            row = new OnboardingDraft { FacilityId = facilityId };
            _dbContext.OnboardingDrafts.Add(row);
        }

        // Always stamped with the version this build writes, never the version it read. A migrated
        // document is only actually upgraded once it is written back at the new version.
        row.SchemaVersion = DraftSchema.CurrentVersion;
        row.DraftJson = DraftSchema.Write(draft.State);
        row.UnlockedStepsJson = JsonSerializer.Serialize(draft.UnlockedStepIds);
        row.UpdatedOn = DateTime.UtcNow;
        row.UpdatedBy = _userContext.ExternalUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Tolerates a malformed value for the same reason DraftSchema.Read does: an unreadable unlock
    // set costs the user some re-navigation, whereas throwing would deny access to a facility whose
    // configuration is intact in Link.
    private IReadOnlyList<string> ReadUnlockedSteps(string? json, string facilityId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, StepIdOptions) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Unlocked step list for facility {FacilityId} could not be parsed; treating it as empty.", facilityId);
            return [];
        }
    }
}
