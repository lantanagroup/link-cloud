using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SystemTask = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Core
{
    [Collection("ReportIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class MeasureReportAggregatorTests : IClassFixture<ReportIntegrationTestFixture>
    {
        private readonly ReportIntegrationTestFixture _fixture;

        private const string AggregateMeasureReportProfile =
            "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/subjectlist-measurereport";

        public MeasureReportAggregatorTests(ReportIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        private static ReportScheduleModel BuildSchedule(
            string facilityId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            return new ReportScheduleModel
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                ReportStartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                ReportEndDate = endDate ?? DateTime.UtcNow,
                Frequency = Frequency.Monthly,
                ReportTypes = { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                CreateDate = DateTime.UtcNow
            };
        }

        private static string BuildPopulationCodeJson(string code, string display,
            string system = "http://terminology.hl7.org/CodeSystem/measure-population")
        {
            var concept = new CodeableConcept
            {
                Coding = new System.Collections.Generic.List<Coding>
                {
                    new Coding { System = system, Code = code, Display = display }
                },
                Text = display
            };
            return JsonSerializer.Serialize(concept, LinkFhirSerializerOptions.ForFhirLenientSerialization);
        }

        /// <summary>
        /// Inserts a ReportSchedule entity directly into the DB context without invoking
        /// the manager which would otherwise auto-create ReportPopulation records.
        /// </summary>
        private static async SystemTask SeedScheduleAsync(ReportDbContext context, ReportScheduleModel schedule)
        {
            var entity = new ReportSchedule
            {
                Id = schedule.Id,
                CreateDate = DateTime.UtcNow,
                FacilityId = schedule.FacilityId,
                ReportStartDate = schedule.ReportStartDate,
                ReportEndDate = schedule.ReportEndDate,
                Frequency = schedule.Frequency,
                Status = schedule.Status
            };

            foreach (var rt in schedule.ReportTypes)
            {
                entity.ReportTypes.Add(new ScheduleReportType { ReportType = rt });
            }

            context.ReportSchedule.Add(entity);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        /// <summary>
        /// Seeds one <see cref="ReportPopulation"/> with its
        /// <see cref="GroupPopulation"/> and optional <see cref="MeasureReportPopulation"/>
        /// children directly into the <see cref="ReportDbContext"/>.
        /// </summary>
        private static async SystemTask SeedReportPopulationAsync(
            ReportDbContext context,
            Guid reportScheduleId,
            string facilityId,
            string measure,
            string reportType,
            (string populationId, string codeJson, int count, string[] measureReportIds)[] groups)
        {
            var population = new ReportPopulation
            {
                Id = Guid.NewGuid(),
                ReportScheduleId = reportScheduleId,
                FacilityId = facilityId,
                Measure = measure,
                ReportType = reportType,
                CreateDate = DateTime.UtcNow
            };

            foreach (var (populationId, codeJson, count, measureReportIds) in groups)
            {
                var groupPopulation = new GroupPopulation
                {
                    PopulationId = populationId,
                    PopulationCodeJson = codeJson,
                    TotalPopulationCount = count
                };

                foreach (var mrId in measureReportIds)
                {
                    groupPopulation.MeasureReportPopulations.Add(new MeasureReportPopulation
                    {
                        MeasureReportId = mrId,
                        PopulationCount = 1
                    });
                }

                population.GroupPopulations.Add(groupPopulation);
            }

            context.ReportPopulation.Add(population);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        private MeasureReportAggregator CreateAggregator(Microsoft.Extensions.DependencyInjection.IServiceScope scope)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<MeasureReportAggregator>>();
            var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
            return new MeasureReportAggregator(logger, reportPopulationManager);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_NoPopulations_ReturnsEmptyList()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var schedule = BuildSchedule("agg-facility-001");
            await SeedScheduleAsync(context, schedule);

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-001");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_SinglePopulation_ReturnsOneMeasureReport()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-002";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     5,
                     Array.Empty<string>())
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-002");

            Assert.Single(result);
            var report = result[0];
            Assert.Equal(MeasureReport.MeasureReportType.SubjectList, report.Type);
            Assert.Equal(MeasureReport.MeasureReportStatus.Complete, report.Status);
            Assert.Equal("http://example.com/Measure/DE-111", report.Measure);
            Assert.Equal("Organization/org-002", report.Reporter?.Reference);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_SinglePopulation_SetsCorrectProfile()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-003";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     3,
                     Array.Empty<string>())
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-003");

            var report = Assert.Single(result);
            Assert.NotNull(report.Meta);
            Assert.Contains(AggregateMeasureReportProfile, report.Meta.Profile);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_SinglePopulation_SetsPeriodFromScheduleDates()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-004";
            var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
            var schedule = BuildSchedule(facilityId, startDate, endDate);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     2,
                     Array.Empty<string>())
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-004");

            var report = Assert.Single(result);
            Assert.NotNull(report.Period);

            // The aggregator sets Period using new FhirDateTime(new DateTimeOffset(date))
            // Compare the generated string representations
            var expectedStart = new FhirDateTime(new DateTimeOffset(startDate)).ToString();
            var expectedEnd = new FhirDateTime(new DateTimeOffset(endDate)).ToString();

            Assert.Equal(expectedStart, report.Period.StartElement?.ToString());
            Assert.Equal(expectedEnd, report.Period.EndElement?.ToString());
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_MultiplePopulations_ReturnsOneReportPerPopulation()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-005";
            var schedule = BuildSchedule(facilityId);
            schedule.ReportTypes = new System.Collections.Generic.List<string> { "DE-111", "DE-222" };
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     2, Array.Empty<string>())
                });

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-222",
                reportType: "DE-222",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     4, Array.Empty<string>())
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-005");

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.Measure == "http://example.com/Measure/DE-111");
            Assert.Contains(result, r => r.Measure == "http://example.com/Measure/DE-222");
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_MultipleGroupPopulations_CreatesGroupForEach()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-006";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     10, Array.Empty<string>()),
                    ("numerator",
                     BuildPopulationCodeJson("numerator", "Numerator"),
                     3, Array.Empty<string>()),
                    ("denominator",
                     BuildPopulationCodeJson("denominator", "Denominator"),
                     7, Array.Empty<string>()),
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-006");

            var report = Assert.Single(result);
            Assert.Equal(3, report.Group.Count);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_GroupPopulationCounts_AreSetOnGroupComponents()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-007";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     42, Array.Empty<string>()),
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-007");

            var report = Assert.Single(result);
            var group = Assert.Single(report.Group);
            var population = Assert.Single(group.Population);
            Assert.Equal(42, population.Count);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_GroupComponent_SubjectResultsRefMatchesPopulationId()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-008";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     5, Array.Empty<string>()),
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-008");

            var report = Assert.Single(result);
            var group = Assert.Single(report.Group);
            var population = Assert.Single(group.Population);
            Assert.Equal("#initial-population-list", population.SubjectResults?.Reference);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_WithMeasureReportPopulations_AddsContainedListWithEntries()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-009";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            var mrId1 = Guid.NewGuid().ToString();
            var mrId2 = Guid.NewGuid().ToString();

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     2,
                     new[] { mrId1, mrId2 })
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-009");

            var report = Assert.Single(result);

            var containedLists = report.Contained.OfType<Hl7.Fhir.Model.List>().ToList();
            Assert.Single(containedLists);

            var list = containedLists[0];
            Assert.Equal(2, list.Entry.Count);
            Assert.Contains(list.Entry, e => e.Item?.Reference == $"MeasureReport/{mrId1}");
            Assert.Contains(list.Entry, e => e.Item?.Reference == $"MeasureReport/{mrId2}");
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_MultipleGroupPopulations_ContainsOneListPerGroup()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-010";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     3, new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() }),
                    ("numerator",
                     BuildPopulationCodeJson("numerator", "Numerator"),
                     1, new[] { Guid.NewGuid().ToString() }),
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-010");

            var report = Assert.Single(result);
            var lists = report.Contained.OfType<Hl7.Fhir.Model.List>().ToList();
            Assert.Equal(2, lists.Count);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_GroupWithNoMeasureReportPopulations_AddsEmptyContainedList()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-011";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     0, Array.Empty<string>()),
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-011");

            var report = Assert.Single(result);
            var list = report.Contained.OfType<Hl7.Fhir.Model.List>().Single();
            Assert.Empty(list.Entry);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_SetsReporterWithOrganizationId()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-012";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     1, Array.Empty<string>()),
                });

            const string orgId = "my-special-org-999";
            var result = await aggregator.CreateMeasureReportAggregate(schedule, orgId);

            var report = Assert.Single(result);
            Assert.Equal($"Organization/{orgId}", report.Reporter?.Reference);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_MultiplePopulations_EachReportHasUniqueId()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-013";
            var schedule = BuildSchedule(facilityId);
            schedule.ReportTypes = new System.Collections.Generic.List<string> { "DE-111", "DE-222", "DE-333" };
            await SeedScheduleAsync(context, schedule);

            foreach (var (measure, reportType) in new[]
            {
                ("http://example.com/Measure/DE-111", "DE-111"),
                ("http://example.com/Measure/DE-222", "DE-222"),
                ("http://example.com/Measure/DE-333", "DE-333")
            })
            {
                await SeedReportPopulationAsync(context, schedule.Id, facilityId, measure, reportType,
                    groups: new[]
                    {
                        ("initial-population",
                         BuildPopulationCodeJson("initial-population", "Initial Population"),
                         1, Array.Empty<string>()),
                    });
            }

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-013");

            Assert.Equal(3, result.Count);
            var ids = result.Select(r => r.Id).ToList();
            Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_GroupComponent_PopulationCodeIsDeserialized()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-014";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            const string populationCode = "numerator";
            const string populationDisplay = "Numerator";

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-111",
                reportType: "DE-111",
                groups: new[]
                {
                    (populationCode,
                     BuildPopulationCodeJson(populationCode, populationDisplay),
                     8, Array.Empty<string>()),
                });
            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-014");

            var report = Assert.Single(result);
            var group = Assert.Single(report.Group);
            var pop = Assert.Single(group.Population);

            Assert.NotNull(pop.Code);
            Assert.Contains(pop.Code.Coding, c => c.Code == populationCode);
            Assert.Contains(pop.Code.Coding, c => c.Display == populationDisplay);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_PopulationsFromDifferentSchedule_AreNotIncluded()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-015";

            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            var otherSchedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, otherSchedule);

            // Population seeded for the OTHER schedule only
            await SeedReportPopulationAsync(context, otherSchedule.Id, facilityId,
                measure: "http://example.com/Measure/DE-999",
                reportType: "DE-999",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     100, Array.Empty<string>()),
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-015");

            Assert.Empty(result);
        }

        [Fact]
        public async SystemTask CreateMeasureReportAggregate_MeasureValue_IsSetFromPopulationMeasure()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var aggregator = CreateAggregator(scope);
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

            var facilityId = "agg-facility-016";
            var schedule = BuildSchedule(facilityId);
            await SeedScheduleAsync(context, schedule);

            const string expectedMeasure = "http://example.com/Measure/CUSTOM-MEASURE";

            await SeedReportPopulationAsync(context, schedule.Id, facilityId,
                measure: expectedMeasure,
                reportType: "CUSTOM",
                groups: new[]
                {
                    ("initial-population",
                     BuildPopulationCodeJson("initial-population", "Initial Population"),
                     3, Array.Empty<string>()),
                });

            var result = await aggregator.CreateMeasureReportAggregate(schedule, "org-016");

            var report = Assert.Single(result);
            Assert.Equal(expectedMeasure, report.Measure);
        }
    }
}
