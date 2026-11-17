namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// The Census service's acquisition schedule for a facility.
//
// Enabled is deliberately not exposed as a plain setter here. It's an arming switch — setting it
// true registers a Quartz job that pulls the census on the configured cron — and it is set once,
// by EnableAsync below, called only from the completion fan-out at the end of onboarding, never
// by a step.
public interface ICensusConfigurationGateway
{
    Task<string?> GetAcquisitionFrequencyAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SaveAcquisitionFrequencyAsync(string facilityId, string acquisitionFrequency, CancellationToken cancellationToken = default);

    // Arms the facility's Census acquisition job. Idempotent — a facility already enabled is left
    // alone rather than re-saved, so a retried completion attempt cannot flap the schedule.
    Task EnableAsync(string facilityId, CancellationToken cancellationToken = default);
}
