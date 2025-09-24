using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace DataAcquisition.Domain.Application.Services;

public interface IRetryIncrementService
{
    Task IncrementRetryAndSave(DataAcquisitionLog? log, CancellationToken cancellationToken = default);
}

public class RetryIncrementService : IRetryIncrementService
{
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;

    public RetryIncrementService(IDataAcquisitionLogManager dataAcquisitionLogManager)
    {
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
    }

    public async Task IncrementRetryAndSave(DataAcquisitionLog? log, CancellationToken cancellationToken = default)
    {
        if (log != null)
        {
            log.RetryAttempts = (log.RetryAttempts ?? 0) + 1;
            log.Status = RequestStatus.Pending;
            log.Notes.Add($"[{DateTime.UtcNow}] Incrementing retry attempts to {log.RetryAttempts}. Setting status back to Pending.");
            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
        }
    }
}
