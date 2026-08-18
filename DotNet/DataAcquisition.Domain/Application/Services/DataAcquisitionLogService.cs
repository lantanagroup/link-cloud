using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IDataAcquisitionLogService
{
    Task StartRetrievalProcess(long logId, CancellationToken cancellationToken = default);
    Task StartRetrievalProcessBulk(List<long> logIds, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogService : IDataAcquisitionLogService
{
    private readonly ILogger<DataAcquisitionLogService> _logger;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;
    IProducer<long, ReadyToAcquire> _readyToAcquireProducer;

    public DataAcquisitionLogService(ILogger<DataAcquisitionLogService> logger, IDataAcquisitionLogManager dataAcquisitionLogManager,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        IProducer<long, ReadyToAcquire> readyToAcquireProducer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(_dataAcquisitionLogManager));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ?? throw new ArgumentNullException(nameof(_dataAcquisitionLogQueries));
        _readyToAcquireProducer = readyToAcquireProducer ?? throw new ArgumentNullException(nameof(readyToAcquireProducer));
    }
    public async Task StartRetrievalProcess(long logId, CancellationToken cancellationToken = default)
    {
        if (logId == default)
        {
            throw new InvalidOperationException(nameof(logId));
        }

        var log = await _dataAcquisitionLogQueries.GetAsync(logId, cancellationToken);

        if (log == null)
        {
            throw new DataAcquisitionLogNotFoundException($"Data acquisition log with ID {logId} not found.");
        }

        var request = new UpdateDataAcquisitionLogModel
        {
            Id = log.Id,
            Status = RequestStatus.Ready,
        };

        object? transaction = null;
        try
        {
            transaction = new object();

            await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);

            await _readyToAcquireProducer.ProduceAsync(
                nameof(KafkaTopic.ReadyToAcquire),
                new Message<long, ReadyToAcquire>
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
            if (transaction != null && ex is ProduceException<string, ReadyToAcquire>)
            {
                //ensure that db update is rolled back
                request.Status = RequestStatus.Failed;
                await _dataAcquisitionLogManager.UpdateAsync(request, cancellationToken);
            }

            _logger.LogError(ex, "Encountered error triggering workflow for log id: {requestId}", request.Id);
            throw;
        }
    }

    public async Task StartRetrievalProcessBulk(List<long> logIds, CancellationToken cancellationToken = default)
    {
        if (logIds == null || !logIds.Any())
        {
            return;
        }

        int successCount = 0;
        int failureCount = 0;
        Exception? firstException = null;

        foreach (var logId in logIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await StartRetrievalProcess(logId, cancellationToken);
                successCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureCount++;
                firstException ??= ex;
                _logger.LogDebug(ex, "Failed to start retrieval process for log id {logId} during bulk operation.", logId);
            }
        }

        if (failureCount > 0)
        {
            _logger.LogWarning("Bulk retrieval process completed with {successCount} successes and {failureCount} failures. First error: {errorMessage}", successCount, failureCount, firstException?.Message);
        }
        else
        {
            _logger.LogInformation("Bulk retrieval process completed successfully for all {successCount} logs.", successCount);
        }
    }
}
