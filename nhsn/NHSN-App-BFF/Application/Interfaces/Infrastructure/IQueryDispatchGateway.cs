namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Query Dispatch's post-discharge dispatch delay for a facility - what the FHIR step calls
// lagDuration, even though it is a separate Link service from the rest of that section.
public interface IQueryDispatchGateway
{
    Task<string?> GetLagDurationAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SetLagDurationAsync(string facilityId, string lagDuration, CancellationToken cancellationToken = default);
}
