using LantanaGroup.Link.DataAcquisition.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public interface IDataAcquisitionLogService
{
    Task<DataAcquisitionLogModel> GetLogEntryById(string id, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogService : IDataAcquisitionLogService
{
    private readonly ILogger<DataAcquisitionLogService> _logger;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;

    public DataAcquisitionLogService(ILogger<DataAcquisitionLogService> logger, IDataAcquisitionLogManager dataAcquisitionLogManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(_dataAcquisitionLogManager));
    }

    public async Task<DataAcquisitionLogModel> GetLogEntryById(string id, CancellationToken cancellationToken = default)
    {
        return DataAcquisitionLogModel.FromDomain(await _dataAcquisitionLogManager.GetAsync(id, cancellationToken));
    }
}
