using LantanaGroup.Link.DataAcquisition.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public interface IDataAcquisitionLogService
{
    Task<DataAcquisitionLogModel> GetLogEntryById(string id, CancellationToken cancellationToken = default);
    Task<IPagedModel<QueryLogSummaryModel>> GetQueryLogSummariesForFacility(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default);
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

    public async Task<IPagedModel<QueryLogSummaryModel>> GetQueryLogSummariesForFacility(string facilityId, int page, int pageSize, string sortBy, SortOrder sortOrder, CancellationToken cancellationToken = default)
    {
        var result = await _dataAcquisitionLogManager.GetByFacilityIdAsync(facilityId, page, pageSize, sortBy, sortOrder, cancellationToken);
        return new QueryLogSummaryModelResponse
        {
            Records = result.Item1.Select(QueryLogSummaryModel.FromDomain).ToList(),
            Metadata = result.Item2
        };
    }
}
