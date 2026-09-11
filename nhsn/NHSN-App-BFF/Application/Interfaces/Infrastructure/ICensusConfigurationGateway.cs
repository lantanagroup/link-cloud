namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// The Census service's acquisition schedule for a facility.
//
// Enabled is deliberately not exposed here. It's an arming switch — setting it true registers a
// Quartz job that pulls the census on the configured cron — and it is set once, by the completion
// fan-out at the end of onboarding, not by any step.
public interface ICensusConfigurationGateway
{
    Task<string?> GetAcquisitionFrequencyAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SaveAcquisitionFrequencyAsync(string facilityId, string acquisitionFrequency, CancellationToken cancellationToken = default);
}
