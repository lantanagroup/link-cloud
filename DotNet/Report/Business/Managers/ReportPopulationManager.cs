using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Domain.Managers
{
    public interface IReportPopulationManager
    {
        Task<ReportPopulationModel> UpdateAsync(ReportPopulationModel model, CancellationToken cancellationToken);

        Task<ReportPopulationModel> AddAsync(ReportPopulationModel model, CancellationToken cancellationToken);
        Task<List<ReportPopulationModel>> AddWithReportScheduleAsync(ReportScheduleModel reportSchedule, CancellationToken cancellationToken);
        Task<ReportPopulationModel> UpdateAsyncWithAggregateResult(ReportPopulationModel model, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task<ReportPopulationModel> AddAsyncWithAggregateResult(string facilityId, Guid reportScheduleId, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken);

        Task AddRangeAsync(IEnumerable<ReportPopulationModel> models, CancellationToken cancellationToken);

        Task<List<ReportPopulationModel>> FindAsync(Expression<Func<ReportPopulation, bool>> predicate, CancellationToken cancellationToken = default);

        Task<ReportPopulationModel?> SingleOrDefaultAsync(Expression<Func<ReportPopulation, bool>> predicate, CancellationToken cancellationToken = default);

        Task<int> CountNumberOfMeasureReportPopulationsInIP(Guid reportScheduleId, CancellationToken cancellationToken = default);
    }

    public class ReportPopulationManager : IReportPopulationManager
    {
        private readonly ReportDbContext _context;
        private readonly ILogger<ReportPopulationManager> _logger;

        public ReportPopulationManager(ReportDbContext context, ILogger<ReportPopulationManager> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ReportPopulationModel> UpdateAsync(ReportPopulationModel model, CancellationToken cancellationToken)
        {
            var entity = await _context.ReportPopulation
                .Include(r => r.GroupPopulations)
                    .ThenInclude(g => g.MeasureReportPopulations)
                .FirstOrDefaultAsync(r => r.Id == model.Id, cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"ReportPopulation with Id {model.Id} not found");

            entity.ModifyDate = DateTime.UtcNow;
            entity.Measure = model.Measure;
            entity.ReportType = model.ReportType;

            var existingGroups = entity.GroupPopulations.ToList();

            foreach (var groupModel in model.GroupPopulations)
            {
                var existingGroup = existingGroups.FirstOrDefault(g => g.PopulationId == groupModel.PopulationId);

                if (existingGroup == null)
                {
                    var newGroup = new GroupPopulation
                    {
                        PopulationId = groupModel.PopulationId,
                        PopulationCodeJson = groupModel.PopulationCodeJson,
                        TotalPopulationCount = groupModel.TotalPopulationCount
                    };

                    foreach (var mrpModel in groupModel.MeasureReportPopulations)
                    {
                        newGroup.MeasureReportPopulations.Add(new MeasureReportPopulation
                        {
                            MeasureReportId = mrpModel.MeasureReportId,
                            PopulationCount = mrpModel.PopulationCount
                        });
                    }

                    entity.GroupPopulations.Add(newGroup);
                }
                else
                {                   
                    existingGroup.PopulationCodeJson = groupModel.PopulationCodeJson;
                    existingGroup.TotalPopulationCount = groupModel.TotalPopulationCount;

                    var existingMrpIds = existingGroup.MeasureReportPopulations
                        .Select(m => m.MeasureReportId)
                        .ToHashSet(StringComparer.Ordinal);

                    foreach (var mrpModel in groupModel.MeasureReportPopulations)
                    {
                        if (!existingMrpIds.Contains(mrpModel.MeasureReportId))
                        {
                            existingGroup.MeasureReportPopulations.Add(new MeasureReportPopulation
                            {
                                MeasureReportId = mrpModel.MeasureReportId,
                                PopulationCount = mrpModel.PopulationCount
                            });
                        }
                    }

                    existingGroups.Remove(existingGroup);
                }
            }

            foreach (var orphan in existingGroups)
            {
                entity.GroupPopulations.Remove(orphan);
            }

            _context.Update(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return model;
        }

        public async Task<ReportPopulationModel> AddAsync(ReportPopulationModel model, CancellationToken cancellationToken)
        {
            await AddRangeAsync(new[] { model }, cancellationToken);
            return model;
        }

        public async Task AddRangeAsync(IEnumerable<ReportPopulationModel> models, CancellationToken cancellationToken)
        {
            if (models == null || !models.Any()) 
                return;

            var entities = new List<ReportPopulation>();

            foreach (var model in models)
            {
                var entity = new ReportPopulation
                {
                    Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id,
                    CreateDate = DateTime.UtcNow,
                    FacilityId = model.FacilityId,
                    ReportType = model.ReportType,
                    ReportScheduleId = model.ReportScheduleId,
                    Measure = model.Measure
                };

                foreach (var groupModel in model.GroupPopulations)
                {
                    entity.GroupPopulations.Add(new GroupPopulation
                    {
                        PopulationId = groupModel.PopulationId,
                        PopulationCodeJson = groupModel.PopulationCodeJson,
                        TotalPopulationCount = groupModel.TotalPopulationCount,
                        MeasureReportPopulations = groupModel.MeasureReportPopulations.Select(mrp => new MeasureReportPopulation
                        {
                            MeasureReportId = mrp.MeasureReportId,
                            PopulationCount = mrp.PopulationCount
                        }).ToList()
                    });
                }

                entities.Add(entity);
            }

            await _context.AddRangeAsync(entities, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var entityList = entities.ToList();
            int index = 0;
            foreach (var model in models)
            {
                model.Id = entityList[index++].Id;
            }
        }

        public async Task<List<ReportPopulationModel>> AddWithReportScheduleAsync(ReportScheduleModel reportSchedule, CancellationToken cancellationToken)
        {
            List<ReportPopulation> models = new();

            foreach (var reportType in reportSchedule.ReportTypes)
            {
                var model = new ReportPopulation
                {
                    Id = Guid.NewGuid(),
                    CreateDate = DateTime.UtcNow,
                    FacilityId = reportSchedule.FacilityId,
                    ReportType = reportType,
                    Measure = reportType, //TODO: Daniel - 03/2026 - We'll want to eventually populate this with the actual measure name rather than the reportType. This will get overridden when there is a patient that meets the IP, but will display as reportType in the manifest file if there are no patients that meet the IP which isn't great, but is a small enough edge case for now. 
                    ReportScheduleId = reportSchedule.Id,
                    GroupPopulations = new List<GroupPopulation>
                    {
                        new GroupPopulation { PopulationId = "initial-population", TotalPopulationCount = 0, PopulationCodeJson = "{}" }
                    }
                };

                models.Add(model);
            }

            await _context.AddRangeAsync(models);
            await _context.SaveChangesAsync(cancellationToken);

            return await FindAsync(rp => rp.ReportScheduleId == reportSchedule.Id, cancellationToken);
        }

        public async Task<ReportPopulationModel> AddAsyncWithAggregateResult(string facilityId, Guid reportScheduleId, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken)
        {
            var model = new ReportPopulationModel
            {
                Id = Guid.NewGuid(),
                Measure = aggregateResult.Measure,
                ReportType = aggregateResult.ReportType,
                CreateDate = DateTime.UtcNow,
                FacilityId = facilityId,
                ReportScheduleId = reportScheduleId
            };

            foreach (var aggregate in aggregateResult.PopulationList)
            {
                if (string.IsNullOrWhiteSpace(aggregate.PopulationId))
                    continue;

                var populationCode = JsonSerializer.Serialize(aggregate.PopulationCode, LinkFhirSerializerOptions.ForFhirLenientSerialization);

                model.GroupPopulations.Add(new GroupPopulationModel
                {
                    PopulationId = aggregate.PopulationId,
                    PopulationCodeJson = populationCode,
                    TotalPopulationCount = aggregate.PopulationCount,
                    MeasureReportPopulations = new List<MeasureReportPopulationModel>
                    {
                        new MeasureReportPopulationModel
                        {
                            MeasureReportId = aggregateResult.MeasureReportId,
                            PopulationCount = aggregate.PopulationCount
                        }
                    }
                });
            }

            await AddAsync(model, cancellationToken);
            return model;
        }

        public async Task<ReportPopulationModel> UpdateAsyncWithAggregateResult(ReportPopulationModel model, AggregateMeasureReportResult aggregateResult, CancellationToken cancellationToken)
        {
            model.ModifyDate = DateTime.UtcNow;
            model.Measure = aggregateResult.Measure;

            foreach (var aggregate in aggregateResult.PopulationList)
            {
                if (string.IsNullOrWhiteSpace(aggregate.PopulationId))
                    continue;

                var group = model.GroupPopulations.FirstOrDefault(x => x.PopulationId == aggregate.PopulationId);

                var populationCode = JsonSerializer.Serialize(aggregate.PopulationCode, LinkFhirSerializerOptions.ForFhirLenientSerialization);

                if (group == null)
                {
                    group = new GroupPopulationModel
                    {
                        PopulationId = aggregate.PopulationId,
                        PopulationCodeJson = populationCode,
                        TotalPopulationCount = aggregate.PopulationCount,
                        MeasureReportPopulations = new List<MeasureReportPopulationModel>()
                    };
                    model.GroupPopulations.Add(group);
                }
                else
                {
                    group.PopulationCodeJson = populationCode;
                    group.TotalPopulationCount += aggregate.PopulationCount;
                }

                if(!group.MeasureReportPopulations.Any(mrp => mrp.MeasureReportId == aggregateResult.MeasureReportId))
                {
                    group.MeasureReportPopulations.Add(new MeasureReportPopulationModel
                    {
                        MeasureReportId = aggregateResult.MeasureReportId,
                        PopulationCount = aggregate.PopulationCount
                    });
                }
            }

            await UpdateAsync(model, cancellationToken);
            return model;
        }

        public async Task<List<ReportPopulationModel>> FindAsync(Expression<Func<ReportPopulation, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _context.ReportPopulation
                .Where(predicate)
                .Select(rp => new ReportPopulationModel
                {
                    Id = rp.Id,
                    CreateDate = rp.CreateDate,
                    ModifyDate = rp.ModifyDate,
                    FacilityId = rp.FacilityId,
                    ReportScheduleId = rp.ReportScheduleId,
                    Measure = rp.Measure,
                    ReportType = rp.ReportType,
                    GroupPopulations = rp.GroupPopulations.Select(gp => new GroupPopulationModel
                    {
                        PopulationId = gp.PopulationId,
                        PopulationCodeJson = gp.PopulationCodeJson,
                        TotalPopulationCount = gp.TotalPopulationCount,
                        MeasureReportPopulations = gp.MeasureReportPopulations.Select(mrp => new MeasureReportPopulationModel
                        {
                            Id = mrp.Id,
                            GroupPopulationId = mrp.GroupPopulationId,
                            MeasureReportId = mrp.MeasureReportId,
                            PopulationCount = mrp.PopulationCount
                        }).ToList()
                    }).ToList()
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<ReportPopulationModel?> SingleOrDefaultAsync(Expression<Func<ReportPopulation, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _context.ReportPopulation
                .Where(predicate)
                .Select(rp => new ReportPopulationModel
                {
                    Id = rp.Id,
                    CreateDate = rp.CreateDate,
                    ModifyDate = rp.ModifyDate,
                    FacilityId = rp.FacilityId,
                    ReportScheduleId = rp.ReportScheduleId,
                    Measure = rp.Measure,
                    ReportType = rp.ReportType,
                    GroupPopulations = rp.GroupPopulations.Select(gp => new GroupPopulationModel
                    {
                        PopulationId = gp.PopulationId,
                        PopulationCodeJson = gp.PopulationCodeJson,
                        TotalPopulationCount = gp.TotalPopulationCount,
                        MeasureReportPopulations = gp.MeasureReportPopulations.Select(mrp => new MeasureReportPopulationModel
                        {
                            Id = mrp.Id,
                            GroupPopulationId = mrp.GroupPopulationId,
                            MeasureReportId = mrp.MeasureReportId,
                            PopulationCount = mrp.PopulationCount
                        }).ToList()
                    }).ToList()
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<int> CountNumberOfMeasureReportPopulationsInIP(Guid reportScheduleId, CancellationToken cancellationToken = default)
        {
            return await _context.ReportPopulation
                .Where(x => x.ReportScheduleId == reportScheduleId)
                .SelectMany(rp => rp.GroupPopulations)
                .Where(gp => gp.PopulationId == "initial-population")
                .SelectMany(gp => gp.MeasureReportPopulations)
                .CountAsync(cancellationToken);
        }
    }
}