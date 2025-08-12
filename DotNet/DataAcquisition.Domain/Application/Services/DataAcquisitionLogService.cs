using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IDataAcquisitionLogService
{
    Task StartRetrievalProcess(string logId, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogService : IDataAcquisitionLogService
{
    private readonly ILogger<DataAcquisitionLogService> _logger;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    IProducer<string, ReadyToAcquire> _readyToAcquireProducer;

    public DataAcquisitionLogService(ILogger<DataAcquisitionLogService> logger, IDataAcquisitionLogManager dataAcquisitionLogManager, IProducer<string, ReadyToAcquire> readyToAcquireProducer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(_dataAcquisitionLogManager));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
    }
    public async Task StartRetrievalProcess(string logId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(logId))
        {
            throw new ArgumentNullException(nameof(logId));
        }

        var log = await _dataAcquisitionLogManager.GetAsync(logId, cancellationToken);

        if (log == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {logId} not found.");
        }

        var request = new UpdateDataAcquisitionLogModel
        {
            Id = log.Id,
            Status = RequestStatusModel.Ready,
        };

        object? transaction = null;
        try
        {
            transaction = new object();

            await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);

            await _readyToAcquireProducer.ProduceAsync(
                nameof(KafkaTopic.ReadyToAcquire),
                new Message<string, ReadyToAcquire>
                {
                    Key = log.Id,
                    Value = new ReadyToAcquire
                    {
                        LogId = log.Id,
                        FacilityId = log.FacilityId
                    }
                }, cancellationToken);

            transaction = null;
        }
        catch (Exception ex)
        {
            if(transaction != null && ex is ProduceException<string, ReadyToAcquire>)
            {
                //ensure that db update is rolled back
                request.Status = RequestStatusModel.Failed;
                await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
            }

            _logger.LogError(ex, "Encountered error triggering workflow for log id: {requestId}", request.Id);
            throw;
        }
    }
}
