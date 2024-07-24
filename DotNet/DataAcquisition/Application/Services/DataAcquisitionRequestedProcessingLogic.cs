﻿using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public class DataAcquisitionRequestedProcessingLogic : IConsumerLogic<string, DataAcquisitionRequested, string, ResourceAcquired>
{
    private readonly ILogger<DataAcquisitionRequestedProcessingLogic> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    public DataAcquisitionRequestedProcessingLogic(ILogger<DataAcquisitionRequestedProcessingLogic> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    public ConsumerConfig createConsumerConfig()
    {
        var settings = new ConsumerConfig
        {
            EnableAutoCommit = false,
            GroupId = ServiceActivitySource.ServiceName
        };
        return settings;
    }

    public async Task executeLogic(ConsumeResult<string, DataAcquisitionRequested> consumeResult, CancellationToken cancellationToken = default, params object[] optionalArgList)
    {
        string correlationId;
        string facilityId;
        var scope = _serviceScopeFactory.CreateScope();
        var patientDataService = scope.ServiceProvider.GetRequiredService<IPatientDataService>();

        try
        {
            correlationId = extractCorrelationId(consumeResult);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "CorrelationId is missing from the message headers.");
            throw new DeadLetterException("CorrelationId is missing from the message headers.", ex);
        }

        try
        {
            facilityId = extractFacilityId(consumeResult);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex, "FacilityId is missing from the message key.");
            throw new DeadLetterException("FacilityId is missing from the message key.", ex);
        }

        List<IBaseMessage> results = new List<IBaseMessage>();
        try
        {
            await patientDataService.Get(new GetPatientDataRequest
            {
                ConsumeResult = consumeResult,
                FacilityId = facilityId,
                CorrelationId = correlationId,
            }, cancellationToken);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            throw new TransientException("Facility configuration is missing: " + ex.Message, ex);
        }
        catch (FhirApiFetchFailureException ex)
        {
            throw new TransientException("Error fetching FHIR API: " + ex.Message, ex);
        }
        catch (ProduceException<string, object> ex)
        {
            throw new TransientException($"Failed to produce message to {consumeResult.Topic} for {facilityId}", ex);
        }
        catch (Exception ex)
        {
            throw new TransientException("Error processing message: " + ex.Message, ex);
        }
    }

    public string extractFacilityId(ConsumeResult<string, DataAcquisitionRequested> consumeResult)
    {
        var facilityId = consumeResult.Message.Key;

        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException("FacilityId is missing from the message key.");

        return facilityId;
    }

    private string extractCorrelationId(ConsumeResult<string, DataAcquisitionRequested> consumeResult)
    {
        var cIBytes = consumeResult.Headers.FirstOrDefault(x => x.Key.ToLower() == DataAcquisitionConstants.HeaderNames.CorrelationId.ToLower())?.GetValueBytes();

        if (cIBytes == null || cIBytes.Length == 0)
            throw new ArgumentNullException("CorrelationId is missing from the message headers.");


        var correlationId = Encoding.UTF8.GetString(cIBytes);
        return correlationId;
    }
}
