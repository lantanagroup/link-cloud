using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class DataAcquisitionRequestedListener : BaseListener<DataAcquisitionRequested, string, DataAcquisitionRequested, string, ResourceAcquired>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DataAcquisitionRequestedListener(ILogger<BaseListener<DataAcquisitionRequested, string, DataAcquisitionRequested, string, ResourceAcquired>> logger,
        IKafkaConsumerFactory<string, DataAcquisitionRequested> kafkaConsumerFactory,
        ITransientExceptionHandler<string, DataAcquisitionRequested> transientExceptionHandler,
        IDeadLetterExceptionHandler<string, DataAcquisitionRequested> deadLetterExceptionHandler,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerErrorHandler,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ServiceInformation> serviceInformation) : base(logger, kafkaConsumerFactory, deadLetterExceptionHandler, deadLetterConsumerErrorHandler, transientExceptionHandler, serviceInformation)
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

        var scope = _serviceScopeFactory.CreateScope();
        var patientDataService =
            scope.ServiceProvider.GetRequiredService<IPatientDataService>();

        await patientDataService.Get(new GetPatientDataRequest
        {
            ConsumeResult = consumeResult,
            FacilityId = facilityId,
            CorrelationId = correlationId
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
        var facilityId = consumeResult.Message.Key;

        return facilityId;
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

