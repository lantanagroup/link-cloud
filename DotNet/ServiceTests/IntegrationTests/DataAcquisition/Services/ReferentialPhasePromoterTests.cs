using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ScheduledFrequency = LantanaGroup.Link.Shared.Application.Models.Frequency;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Services
{
    [Collection("DataAcquisitionIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ReferentialPhasePromoterTests
    {
        private readonly DataAcquisitionIntegrationTestFixture _fixture;

        public ReferentialPhasePromoterTests(DataAcquisitionIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private static IReferentialPhasePromoter CreatePromoter(IServiceScope scope)
        {
            var logger = new Mock<ILogger<ReferentialPhasePromoter>>().Object;
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            var queryPlanQueries = scope.ServiceProvider.GetRequiredService<IQueryPlanQueries>();
            var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            return new ReferentialPhasePromoter(logger, dbContext, queryPlanQueries, logManager);
        }

        private static async Task<(string facilityId, string correlationId, string reportTrackingId)> SeedCorrelationAsync(
            IServiceScope scope,
            ScheduledFrequency frequency,
            RequestStatus initialStatus,
            IReadOnlyList<(string ResourceType, string ResourceId)> pending,
            Action<Dictionary<string, IQueryConfig>, Dictionary<string, IQueryConfig>>? configurePlan = null)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
            var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
            var queryPlanManager = scope.ServiceProvider.GetRequiredService<IQueryPlanManager>();

            var tag = Guid.NewGuid().ToString("N");
            var facilityId = $"TestFacility_{tag}";
            var correlationId = Guid.NewGuid().ToString();
            var reportTrackingId = Guid.NewGuid().ToString();

            var initial = new Dictionary<string, IQueryConfig>
            {
                { "1", new ParameterQueryConfig
                    {
                        ResourceType = "Patient",
                        Parameters = new List<IParameter>
                        {
                            new LiteralParameter { Name = "id", Literal = "seed-patient" }
                        }
                    }
                }
            };
            var supplemental = new Dictionary<string, IQueryConfig>
            {
                { "1", new ReferenceQueryConfig
                    {
                        ResourceType = "Location",
                        OperationType = OperationType.Search,
                        Paged = 25
                    }
                }
            };

            configurePlan?.Invoke(initial, supplemental);

            await queryPlanManager.AddAsync(new CreateQueryPlanModel
            {
                PlanName = $"PromoterTest_{tag}",
                FacilityId = facilityId,
                EHRDescription = "PromoterTest",
                LookBack = "1d",
                Type = frequency,
                InitialQueries = initial,
                SupplementalQueries = supplemental,
            });

            dbContext.ScheduledReports.Add(new ScheduledReportEntity
            {
                ReportTrackingId = Guid.Parse(reportTrackingId),
                Frequency = frequency,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            await logManager.CreateAsync(new CreateDataAcquisitionLogModel
            {
                FacilityId = facilityId,
                CorrelationId = correlationId,
                ReportTrackingId = reportTrackingId,
                QueryPhase = QueryPhase.Initial,
                QueryType = FhirQueryType.Read,
                Status = initialStatus,
                Priority = AcquisitionPriority.Normal,
                ReportableEvent = ReportableEvent.Adhoc
            });

            if (pending.Count > 0)
            {
                var refMgr = scope.ServiceProvider.GetRequiredService<IReferenceResourcesManager>();
                await refMgr.StagePendingReferencesAsync(facilityId, correlationId, pending);
            }

            return (facilityId, correlationId, reportTrackingId);
        }

        [Fact]
        public async Task PromoteAsync_CreatesReferentialLogAndPurgesStagedRows()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            var (facilityId, correlationId, _) = await SeedCorrelationAsync(
                scope,
                ScheduledFrequency.Daily,
                RequestStatus.Completed,
                new[]
                {
                    ("Location", "loc-1"),
                    ("Location", "loc-2"),
                });

            var promoter = CreatePromoter(scope);

            // Act
            var created = await promoter.PromoteAsync(facilityId, correlationId);

            // Assert — one referential log created
            Assert.Equal(1, created);

            var referentialLog = await dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .Include(l => l.FhirQueries).ThenInclude(q => q.FhirQueryResourceTypes)
                .SingleAsync(l => l.FacilityId == facilityId
                              && l.CorrelationId == correlationId
                              && l.QueryPhase == QueryPhase.Referential);

            Assert.Equal(RequestStatus.Pending, referentialLog.Status);
            Assert.Equal(1, referentialLog.SiblingCount);
            Assert.Single(referentialLog.FhirQueries);

            var fhirQuery = referentialLog.FhirQueries.Single();
            Assert.True(fhirQuery.IsReference);
            Assert.Equal(FhirQueryType.Search, fhirQuery.QueryType);
            Assert.Equal(25, fhirQuery.Paged);
            Assert.Equal(new[] { "loc-1", "loc-2" }, fhirQuery.IdQueryParameterValues.OrderBy(x => x).ToArray());
            Assert.Single(fhirQuery.FhirQueryResourceTypes);
            Assert.Equal(Hl7.Fhir.Model.ResourceType.Location, fhirQuery.FhirQueryResourceTypes.Single().ResourceType);

            // Staging table drained
            var remainingPending = await dbContext.PendingReferenceIds
                .AsNoTracking()
                .CountAsync(p => p.FacilityId == facilityId && p.CorrelationId == correlationId);
            Assert.Equal(0, remainingPending);
        }

        [Fact]
        public async Task PromoteAsync_CreatesOneLogPerResourceType()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            var (facilityId, correlationId, _) = await SeedCorrelationAsync(
                scope,
                ScheduledFrequency.Daily,
                RequestStatus.Completed,
                new[]
                {
                    ("Location", "loc-1"),
                    ("Medication", "med-1"),
                    ("Medication", "med-2"),
                },
                configurePlan: (_, supplemental) =>
                {
                    supplemental["2"] = new ReferenceQueryConfig
                    {
                        ResourceType = "Medication",
                        OperationType = OperationType.SearchPost,
                        Paged = 50
                    };
                });

            var promoter = CreatePromoter(scope);

            var created = await promoter.PromoteAsync(facilityId, correlationId);

            Assert.Equal(2, created);

            var referentialLogs = await dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .Include(l => l.FhirQueries).ThenInclude(q => q.FhirQueryResourceTypes)
                .Where(l => l.FacilityId == facilityId
                         && l.CorrelationId == correlationId
                         && l.QueryPhase == QueryPhase.Referential)
                .ToListAsync();

            Assert.Equal(2, referentialLogs.Count);
            Assert.All(referentialLogs, l => Assert.Equal(2, l.SiblingCount));

            var byType = referentialLogs.ToDictionary(
                l => l.FhirQueries.Single().FhirQueryResourceTypes.Single().ResourceType.ToString());

            Assert.Equal(FhirQueryType.Search, byType["Location"].QueryType);
            Assert.Equal(new[] { "loc-1" }, byType["Location"].FhirQueries.Single().IdQueryParameterValues.ToArray());

            Assert.Equal(FhirQueryType.SearchPost, byType["Medication"].QueryType);
            Assert.Equal(new[] { "med-1", "med-2" }, byType["Medication"].FhirQueries.Single().IdQueryParameterValues.OrderBy(x => x).ToArray());
        }

        [Fact]
        public async Task PromoteAsync_IsIdempotentWhenReferentialLogAlreadyExists()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            var (facilityId, correlationId, _) = await SeedCorrelationAsync(
                scope,
                ScheduledFrequency.Daily,
                RequestStatus.Completed,
                new[] { ("Location", "loc-1") });

            var promoter = CreatePromoter(scope);

            await promoter.PromoteAsync(facilityId, correlationId);

            // Second call after new staged rows are added — must not create a second
            // referential log. Existing referential log "wins" and late stragglers are
            // dropped via PurgePending.
            var refMgr = scope.ServiceProvider.GetRequiredService<IReferenceResourcesManager>();
            await refMgr.StagePendingReferencesAsync(facilityId, correlationId, new[] { ("Location", "loc-2") });

            var created = await promoter.PromoteAsync(facilityId, correlationId);
            Assert.Equal(0, created);

            var referentialCount = await dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .CountAsync(l => l.FacilityId == facilityId
                              && l.CorrelationId == correlationId
                              && l.QueryPhase == QueryPhase.Referential);
            Assert.Equal(1, referentialCount);

            var remaining = await dbContext.PendingReferenceIds
                .AsNoTracking()
                .CountAsync(p => p.FacilityId == facilityId && p.CorrelationId == correlationId);
            Assert.Equal(0, remaining);
        }

        [Fact]
        public async Task FindAndPromoteReadyCorrelations_SkipsCorrelationsWithNonTerminalInitialLogs()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            var (facilityId, correlationId, _) = await SeedCorrelationAsync(
                scope,
                ScheduledFrequency.Daily,
                RequestStatus.Pending, // non-terminal
                new[] { ("Location", "loc-1") });

            var promoter = CreatePromoter(scope);

            var promoted = await promoter.FindAndPromoteReadyCorrelationsAsync(maxCorrelationsPerRun: 50);

            // This correlation must not be picked up; other concurrent test data may
            // contribute to the return value, so we assert no referential log exists
            // for THIS correlation and the pending rows are still in place.
            var referentialCount = await dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .CountAsync(l => l.FacilityId == facilityId
                              && l.CorrelationId == correlationId
                              && l.QueryPhase == QueryPhase.Referential);
            Assert.Equal(0, referentialCount);

            var remaining = await dbContext.PendingReferenceIds
                .AsNoTracking()
                .CountAsync(p => p.FacilityId == facilityId && p.CorrelationId == correlationId);
            Assert.Equal(1, remaining);
        }

        [Fact]
        public async Task FindAndPromoteReadyCorrelations_PromotesCorrelationsWithTerminalInitialLogs()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            var (facilityId, correlationId, _) = await SeedCorrelationAsync(
                scope,
                ScheduledFrequency.Daily,
                RequestStatus.Completed,
                new[] { ("Location", "loc-1") });

            var promoter = CreatePromoter(scope);

            await promoter.FindAndPromoteReadyCorrelationsAsync(maxCorrelationsPerRun: 50);

            var referentialCount = await dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .CountAsync(l => l.FacilityId == facilityId
                              && l.CorrelationId == correlationId
                              && l.QueryPhase == QueryPhase.Referential);
            Assert.Equal(1, referentialCount);

            var remaining = await dbContext.PendingReferenceIds
                .AsNoTracking()
                .CountAsync(p => p.FacilityId == facilityId && p.CorrelationId == correlationId);
            Assert.Equal(0, remaining);
        }

        [Fact]
        public async Task PromoteAsync_DropsStagedIdsForResourceTypesMissingFromPlan()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

            var (facilityId, correlationId, _) = await SeedCorrelationAsync(
                scope,
                ScheduledFrequency.Daily,
                RequestStatus.Completed,
                new[]
                {
                    ("Location", "loc-1"),          // plan has a config for this
                    ("Practitioner", "prac-1"),     // plan has NO config for this
                });

            var promoter = CreatePromoter(scope);

            var created = await promoter.PromoteAsync(facilityId, correlationId);

            Assert.Equal(1, created); // only Location was promoted

            var referentialLogs = await dbContext.DataAcquisitionLogs
                .AsNoTracking()
                .Include(l => l.FhirQueries).ThenInclude(q => q.FhirQueryResourceTypes)
                .Where(l => l.FacilityId == facilityId
                         && l.CorrelationId == correlationId
                         && l.QueryPhase == QueryPhase.Referential)
                .ToListAsync();
            Assert.Single(referentialLogs);
            Assert.Equal(
                Hl7.Fhir.Model.ResourceType.Location,
                referentialLogs.Single().FhirQueries.Single().FhirQueryResourceTypes.Single().ResourceType);

            // All pending rows for this correlation drained regardless of whether
            // a referential log was created for their type.
            var remaining = await dbContext.PendingReferenceIds
                .AsNoTracking()
                .CountAsync(p => p.FacilityId == facilityId && p.CorrelationId == correlationId);
            Assert.Equal(0, remaining);
        }
    }
}
