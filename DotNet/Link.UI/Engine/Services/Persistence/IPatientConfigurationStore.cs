using Automation.UI.Models;

namespace Automation.UI.Services.Persistence;

public interface IPatientConfigurationStore
{
    Task<List<PatientConfiguration>> GetAllAsync(CancellationToken ct = default);
    Task<PatientConfiguration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpsertAsync(PatientConfiguration configuration, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
