using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using MediatR;

using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryPlanConfig;

public class SaveQueryPlanCommand : IRequest<Unit>
{
    public string FacilityId { get; set; }
    public string QueryPlan { get; set; }
    public QueryPlanType QueryPlanType { get; set; }
}

public class SaveQueryPlanCommandHandler : IRequestHandler<SaveQueryPlanCommand, Unit>
{
    private readonly ILogger<SaveQueryPlanCommandHandler> _logger;
    private readonly IQueryPlanRepository _repository;
    private readonly IMediator _mediator;
    private readonly CompareLogic _compareLogic;

    public SaveQueryPlanCommandHandler(ILogger<SaveQueryPlanCommandHandler> logger, IQueryPlanRepository repository, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _compareLogic = new CompareLogic();
        _compareLogic.Config.MaxDifferences = 25;
    }

    private async Task SendAudit(string message, string correlationId, string facilityId, AuditEventType type, List<PropertyChangeModel> changes)
    {
        await _mediator.Send(new TriggerAuditEventCommand
        {
            AuditableEvent = new AuditEventMessage
            {
                FacilityId = facilityId,
                CorrelationId = "",
                Action = type,
                EventDate = DateTime.UtcNow,
                ServiceName = DataAcquisitionConstants.ServiceName,
                PropertyChanges = changes != null ? changes : new List<PropertyChangeModel>(),
                Resource = "DataAcquisition",
                User = "",
                UserId = "",
                Notes = $"{message}"
            }
        });
    }


    public async Task<Unit> Handle(SaveQueryPlanCommand request, CancellationToken cancellationToken)
    {
        if (await _mediator.Send(new CheckIfTenantExistsQuery { TenantId = request.FacilityId }, cancellationToken) == false)
        {
            throw new MissingTenantConfigurationException($"Facility {request.FacilityId} not found.");
        }

        var jsonSettings = new JsonSerializerSettings
        {
            TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
            TypeNameHandling = TypeNameHandling.Auto
        };

        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        Domain.Entities.QueryPlan updatedPlan = null;
        try
        {
            switch (request.QueryPlanType)
            {
                case QueryPlanType.QueryPlans:
                    //runTest();
                    var queryPlan = JsonConvert.DeserializeObject<QueryPlanResult>(request.QueryPlan, jsonSettings);
                    if (queryPlan != null)
                    {
                        var queryPlanResult = await _repository.GetAsync(request.FacilityId, cancellationToken);
                        
                        if (queryPlanResult == null)
                        {

                            await _repository.AddAsync(queryPlan.QueryPlan, cancellationToken);
                            await SendAudit($"Create query plan configuration for '{request.FacilityId}'", null, request.FacilityId, AuditEventType.Create, null);
                        }
                        else
                        {
                            updatedPlan = await _repository.UpdateAsync(queryPlan.QueryPlan, cancellationToken);

                            var resultChanges = _compareLogic.Compare(queryPlanResult, updatedPlan);
                            List<Difference> list = resultChanges.Differences;
                            List<PropertyChangeModel> propertyChanges = new List<PropertyChangeModel>();
                            list.ForEach(d => {
                                propertyChanges.Add(new PropertyChangeModel
                                {
                                    PropertyName = d.PropertyName,
                                    InitialPropertyValue = d.Object2Value,
                                    NewPropertyValue = d.Object1Value
                                });

                            });
                            await SendAudit($"Update query plan configuration {updatedPlan.Id} for '{updatedPlan.FacilityId}'", null, updatedPlan.FacilityId, AuditEventType.Create, propertyChanges);
                        }

                    }
                    break;
                case QueryPlanType.InitialQueries:
                    var initialQueries = JsonConvert.DeserializeObject<InitialQueryResult>(request.QueryPlan);
                    if (initialQueries != null)
                    {
                        await _repository.SaveInitialQueries(request.FacilityId, initialQueries.InitialQueries, cancellationToken);
                        await SendAudit($"Update initial query configuration for {request.FacilityId}", null, request.FacilityId, AuditEventType.Update, null);
                    }
                    break;
                case QueryPlanType.SupplementalQueries:
                    var supplementalQueries = JsonConvert.DeserializeObject<SupplementalQueryResult>(request.QueryPlan);
                    if (supplementalQueries != null) {
                       await _repository.SaveSupplementalQueries(request.FacilityId, supplementalQueries.SupplementalQueries, cancellationToken);
                       await SendAudit($"Update supplemental query configuration for '{request.FacilityId}'", null, request.FacilityId, AuditEventType.Update, null);
                    }
                    break;
            }


        }
        catch(Exception ex)
        {
            throw ex;
        }
        
        return new Unit();
    }

    //private void runTest()
    //{
    //    var testPlan = new QueryPlan
    //    {
    //        _id = "testfacility-QP1",
    //        ReportType = "Test",
    //        FacilityId = "testFacility",
    //        EHRDescription = "Epic",
    //        LookBack = "test",
    //        InitialQueries = new Dictionary<string, Domain.Interfaces.IQueryConfig>
    //        {
    //            {"1", new ParameterQueryConfig
    //                {
    //                    ResourceType = "Encounter",
    //                    Parameters = new List<IParameter>()
    //                } 
    //            }
    //        }
    //    };
        
    //    var jsonSettings = new JsonSerializerSettings
    //    {
    //        NullValueHandling = NullValueHandling.Ignore,
    //        TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
    //        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
    //        TypeNameHandling = TypeNameHandling.Auto
    //    };

    //    var jsonStr = JsonConvert.SerializeObject(testPlan, jsonSettings);
    //    Console.WriteLine(jsonStr);
    //}
}
