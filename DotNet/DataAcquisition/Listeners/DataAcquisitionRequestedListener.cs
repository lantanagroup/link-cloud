using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class DataAcquisitionRequestedListener : BaseListener<DataAcquisitionRequested, string, DataAcquisitionRequested, string, ResourceAcquired>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DataAcquisitionRequestedListener(ILogger<BaseListener<DataAcquisitionRequested, string, DataAcquisitionRequested, string, ResourceAcquired>> logger,
        IKafkaConsumerFactory<string, DataAcquisitionRequested> kafkaConsumerFactory,
        ITransientExceptionHandler<DataAcquisitionRequested, string, DataAcquisitionRequested> transientExceptionHandler,
        IDeadLetterExceptionHandler<DataAcquisitionRequested, string, DataAcquisitionRequested> deadLetterExceptionHandler,
        IDeadLetterExceptionHandler<DataAcquisitionRequested, string, string> deadLetterConsumerErrorHandler,
        IServiceScopeFactory serviceScopeFactory,
        ServiceInformation serviceInformation) : base(logger, kafkaConsumerFactory, deadLetterExceptionHandler, deadLetterConsumerErrorHandler, transientExceptionHandler, serviceInformation)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteListenerAsync(ConsumeResult<string, DataAcquisitionRequested> consumeResult, CancellationToken cancellationToken = default)
    {
        string facilityId;
        string correlationId;
        string reportTrackingId;

        try
        {
            correlationId = ExtractCorrelationId(consumeResult);
        }
        catch (ArgumentNullException ex)
        {
            Logger.LogError(ex, "CorrelationId is missing from the message headers.");
            throw new DeadLetterException("CorrelationId is missing from the message headers.", ex);
        }

        try
        {
            facilityId = ExtractFacilityId(consumeResult);

            if (string.IsNullOrWhiteSpace(facilityId))
                throw new ArgumentNullException("FacilityId is missing from the message key.");
        }
        catch (ArgumentNullException ex)
        {
            Logger.LogError(ex, "FacilityId is missing from the message key.");
            throw new DeadLetterException("FacilityId is missing from the message key.", ex);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var patientDataService =
            scope.ServiceProvider.GetRequiredService<IPatientDataService>();

        await patientDataService.CreateLogEntries(new GetPatientDataRequest
        {
            ConsumeResult = consumeResult,
            FacilityId = facilityId,
            CorrelationId = correlationId,
            QueryPlanType = Enum.Parse<QueryPlanType>(consumeResult.Message.Value.QueryType, true),
        }, cancellationToken);
    }

    protected override ConsumerConfig CreateConsumerConfig()
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = ServiceActivitySource.ServiceName
        };
        return settings;
    }

    protected override string ExtractFacilityId(ConsumeResult<string, DataAcquisitionRequested> consumeResult)
    {
        var key = consumeResult.Message.Key;

        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (key.TrimStart().StartsWith('{'))
        {
            try
            {
                var resourceKey = JsonSerializer.Deserialize<ResourceKey>(key);
                if (resourceKey != null && !string.IsNullOrWhiteSpace(resourceKey.FacilityId))
                {
                    return resourceKey.FacilityId;
                }
            }
            catch (JsonException)
            {
                // Fallback to returning the raw key if it's not a valid ResourceKey JSON
            }
        }

        return key;
    }


    protected override string ExtractCorrelationId(ConsumeResult<string, DataAcquisitionRequested> consumeResult)
    {
        var cIBytes = consumeResult.Headers
            .FirstOrDefault(x => x.Key.ToLower() == DataAcquisitionConstants.HeaderNames.CorrelationId.ToLower())
            ?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            throw new ArgumentNullException("CorrelationId is missing from the message headers.");


        var correlationId = Encoding.UTF8.GetString(cIBytes);
        return correlationId;
    }

}

