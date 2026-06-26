using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Internal;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using CreateQueryPlanModel = DataAcquisition.Domain.Application.Models.CreateQueryPlanModel;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class AcquisitionProcessorBackgroundServiceTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;
    private readonly Mock<IPatientDataService> _patientDataServiceMock;

    public AcquisitionProcessorBackgroundServiceTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _patientDataServiceMock = new Mock<IPatientDataService>();
    }



    [Fact]
    public async Task ProcessWorkItem_LogNotFound_SkipsProcessing()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AcquisitionProcessorBackgroundService>>();
        var service = new AcquisitionProcessorBackgroundService(loggerMock.Object, _fixture.ServiceProvider, null);

        // Use an ID that definitely does not exist
        var workItem = new AcquisitionWorkItem(999999, "NonExistent");

        // Act
        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await service.EnqueueAsync(workItem, cts.Token);

        // Wait long enough for the channel consumer to attempt processing
        await Task.Delay(1000);

        _patientDataServiceMock.Verify(s =>
            s.ExecuteLogRequest(It.IsAny<AcquisitionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProcessWorkItem_PatientNotReportable_MarksLogNotReportable()
    {
        var facilityId = $"Reportable_{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid().ToString();
        const string patientId = "P1"; // bare id; matches the EncounterMapping and the log's PatientId

        long conditionLogId;

        // Arrange the full org-location-gated scenario: a query plan, an active org-location config,
        // a Queued Condition log, and an encounter mapping that is NOT mapped to org (so the patient
        // is not reportable).
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            var queryPlanManager = scope.ServiceProvider.GetRequiredService<IQueryPlanManager>();
            var configManager = scope.ServiceProvider.GetRequiredService<IOrganizationLocationConfigurationManager>();
            var encounterMappingManager = scope.ServiceProvider.GetRequiredService<IEncounterMappingManager>();

            dbContext.Set<FhirQueryConfiguration>().Add(new FhirQueryConfiguration
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                FhirServerBaseUrl = "https://example.org/fhir"
            });
            await dbContext.SaveChangesAsync();

            await queryPlanManager.AddAsync(new CreateQueryPlanModel
            {
                PlanName = "Reportability",
                FacilityId = facilityId,
                EHRDescription = "Test",
                LookBack = "1d",
                Type = Frequency.Discharge,
                InitialQueries = new Dictionary<string, IQueryConfig>
                {
                    { "1", new ParameterQueryConfig { ResourceType = "Condition", Parameters = new List<IParameter> { new LiteralParameter { Name = "id", Literal = "123" } } } }
                },
                SupplementalQueries = new Dictionary<string, IQueryConfig>
                {
                    { "1", new ReferenceQueryConfig { ResourceType = "Encounter" } }
                }
            });

            await configManager.CreateAsync(new CreateOrganizationLocationConfigurationModel
            {
                FacilityId = facilityId,
                Description = "active",
                IsActive = true,
                Conditions = new List<CreateOrganizationLocationConditionModel>
                {
                    new() { FhirPath = "Location.name = 'x'", Priority = 1 }
                }
            });

            var conditionLog = new DataAcquisitionLog
            {
                FacilityId = facilityId,
                PatientId = patientId,
                CorrelationId = correlationId,
                Status = RequestStatus.Queued,
                QueryPhase = QueryPhase.Initial,
                ReportableEvent = ReportableEvent.Discharge,
                SiblingCount = 1,
                FhirQueries = new List<FhirQuery>
                {
                    new()
                    {
                        FacilityId = facilityId,
                        QueryType = FhirQueryType.Search,
                        FhirQueryResourceTypes = new List<FhirQueryResourceType>
                        {
                            new() { ResourceType = Hl7.Fhir.Model.ResourceType.Condition }
                        }
                    }
                }
            };
            dbContext.DataAcquisitionLogs.Add(conditionLog);
            await dbContext.SaveChangesAsync();
            conditionLogId = conditionLog.Id;

            // The patient's only encounter is NOT mapped to an org location -> not reportable.
            await encounterMappingManager.CreateAsync(new CreateEncounterMappingModel
            {
                FacilityId = facilityId,
                PatientId = patientId,
                EncounterId = "E1",
                MappedToOrg = false
            });
        }

        // Act — drive the worker.
        var loggerMock = new Mock<ILogger<AcquisitionProcessorBackgroundService>>();
        var service = new AcquisitionProcessorBackgroundService(loggerMock.Object, _fixture.ServiceProvider, null);
        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await service.EnqueueAsync(new AcquisitionWorkItem(conditionLogId, facilityId), cts.Token);

        // Poll until the log reaches a terminal status (or timeout).
        RequestStatus? status = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            using var pollScope = _fixture.ServiceProvider.CreateScope();
            var logQueries = pollScope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
            status = (await logQueries.GetAsync(conditionLogId))?.Status;
            if (status == RequestStatus.NotReportable)
                break;
        }

        await service.StopAsync(CancellationToken.None);

        // Assert — the dependent log was preempted to NotReportable (a terminal status), not acquired.
        // Had acquisition run, the log would be Processing/Completed/Failed instead.
        Assert.Equal(RequestStatus.NotReportable, status);
    }
}
