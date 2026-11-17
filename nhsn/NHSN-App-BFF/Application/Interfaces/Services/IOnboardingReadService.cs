using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public interface IOnboardingReadService
{
    // Assembles the facility's draft by fanning out across Link, the Facilities row, the draft row
    // and the BFF's tables.
    Task<DraftEnvelopeResponse> GetAsync(CancellationToken cancellationToken = default);
}
