using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Options;
using System.Text;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

namespace LantanaGroup.Link.DataAcquisition.Listeners;

public class PatientCensusScheduledListener : BaseListener<PatientCensusScheduled, string, PatientCensusScheduled, string, List<PatientListModel>>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PatientCensusScheduledListener(ILogger<BaseListener<PatientCensusScheduled, string, PatientCensusScheduled, string, List<PatientListModel>>> logger,
        IKafkaConsumerFactory<string, PatientCensusScheduled> kafkaConsumerFactory,
        ITransientExceptionHandler<string, PatientCensusScheduled> transientExceptionHandler,
        IDeadLetterExceptionHandler<string, PatientCensusScheduled> deadLetterExceptionHandler,
        IDeadLetterExceptionHandler<string, string> deadLetterConsumerErrorHandler,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ServiceInformation> serviceInformation) : base(logger, kafkaConsumerFactory, deadLetterExceptionHandler, deadLetterConsumerErrorHandler, transientExceptionHandler, serviceInformation)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    protected override async Task ExecuteListenerAsync(ConsumeResult<string, PatientCensusScheduled> consumeResult, CancellationToken cancellationToken)
    {
        string facilityId;

        try
        {
            facilityId = ExtractFacilityId(consumeResult);
        }
        catch (ArgumentNullException ex)
        {
            Logger.LogError(ex, "FacilityId is missing from the message key.");
            throw new DeadLetterException("FacilityId is missing from the message key.", ex);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var patientCensusService =
            scope.ServiceProvider.GetRequiredService<IPatientCensusService>();

        List<PatientListModel>? patientLists;
        try
        {
            await patientCensusService.CreateLog(facilityId, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while processing the message.");
            throw new DeadLetterException("Error occurred while processing the message.", ex);
        }
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

    protected override string ExtractFacilityId(ConsumeResult<string, PatientCensusScheduled> consumeResult)
    {
        var facilityId = consumeResult.Message.Key;

        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException("FacilityId is missing from the message key.");

        return facilityId;
    }

    protected override string ExtractCorrelationId(ConsumeResult<string, PatientCensusScheduled> consumeResult)
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

