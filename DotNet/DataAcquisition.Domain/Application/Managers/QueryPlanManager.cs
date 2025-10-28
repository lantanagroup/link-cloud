using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IQueryPlanManager
{
    Task<QueryPlanModel> AddAsync(CreateQueryPlanModel model, CancellationToken cancellationToken = default);
    Task<QueryPlanModel> UpdateAsync(UpdateQueryPlanModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default);
}

public class QueryPlanManager : IQueryPlanManager
{
    private readonly IDatabase _database;
    private readonly ILogger<QueryPlanManager> _logger;

    public QueryPlanManager(IDatabase database, ILogger<QueryPlanManager> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QueryPlanModel> AddAsync(CreateQueryPlanModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "CreateQueryPlanModel cannot be null.");
        }

        // Validate query order
        ValidateQueryOrder(model.InitialQueries, "InitialQueries");
        ValidateQueryOrder(model.SupplementalQueries, "SupplementalQueries");

        var entity = new QueryPlan
        {
            PlanName = model.PlanName,
            FacilityId = model.FacilityId,
            EHRDescription = model.EHRDescription,
            LookBack = model.LookBack,
            InitialQueries = model.InitialQueries,
            SupplementalQueries = model.SupplementalQueries,
            Type = model.Type,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };

        entity = await _database.QueryPlanRepository.AddAsync(entity);
        await _database.QueryPlanRepository.SaveChangesAsync();

        return QueryPlanModel.FromDomain(entity);
    }

    public async Task<QueryPlanModel> UpdateAsync(UpdateQueryPlanModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "UpdateQueryPlanModel cannot be null.");
        }

        // Validate query order
        ValidateQueryOrder(model.InitialQueries, "InitialQueries");
        ValidateQueryOrder(model.SupplementalQueries, "SupplementalQueries");

        var existingQueryPlan = await _database.QueryPlanRepository.FirstOrDefaultAsync(q => q.FacilityId == model.FacilityId && q.Type == model.Type);

        if (existingQueryPlan != null)
        {
            existingQueryPlan.InitialQueries = model.InitialQueries;
            existingQueryPlan.SupplementalQueries = model.SupplementalQueries;
            existingQueryPlan.PlanName = model.PlanName;
            existingQueryPlan.EHRDescription = model.EHRDescription;
            existingQueryPlan.LookBack = model.LookBack;
            existingQueryPlan.ModifyDate = DateTime.UtcNow;

            await _database.QueryPlanRepository.SaveChangesAsync();

            return QueryPlanModel.FromDomain(existingQueryPlan);
        }

        throw new NotFoundException($"No Query Plan for FacilityId {model.FacilityId} and Type {model.Type} was found.");
    }

    public async Task DeleteAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default)
    {
        var entity = await _database.QueryPlanRepository.SingleOrDefaultAsync(q => q.FacilityId == facilityId && q.Type == type);

        if (entity != null)
        {
            _database.QueryPlanRepository.Remove(entity);
            await _database.QueryPlanRepository.SaveChangesAsync();
        }
        else
        {
            throw new NotFoundException($"No Query Plan for FacilityId {facilityId} and Type {type} was found.");
        }
    }

    private void ValidateQueryOrder(Dictionary<string, IQueryConfig> queries, string querySetName)
    {
        if (queries == null) return;

        bool seenReference = false;
        foreach (var kvp in queries.OrderBy(q => int.TryParse(q.Key, out var i) ? i : int.MaxValue))
        {
            var config = kvp.Value;
            if (config is ReferenceQueryConfig)
            {
                seenReference = true;
            }
            else if (config is ParameterQueryConfig && seenReference)
            {
                throw new IncorrectQueryPlanOrderException(
                    $"All ReferenceQueryConfig entries must appear after all ParameterQueryConfig entries in {querySetName}.");
            }
        }
    }
}