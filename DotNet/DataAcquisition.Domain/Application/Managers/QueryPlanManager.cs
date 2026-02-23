using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IQueryPlanManager
{
    Task<QueryPlanModel> AddAsync(CreateQueryPlanModel model, CancellationToken cancellationToken = default);
    Task<QueryPlanModel> UpdateAsync(UpdateQueryPlanModel model, CancellationToken cancellationToken = default);
    Task DeleteAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default);
    Task DeleteAllQueryPlansAsync(string facilityId, CancellationToken cancellationToken = default);
}

public class QueryPlanManager : IQueryPlanManager
{
    private readonly IDatabase _database;
    private readonly ILogger<QueryPlanManager> _logger;
    private readonly IQueryPlanValidator _validator;

    public QueryPlanManager(
        IDatabase database,
        ILogger<QueryPlanManager> logger,
        IQueryPlanValidator validator)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Returns a sanitized version of a value that is safe to include in log messages.
    /// Removes newline characters to help prevent log forging.
    /// </summary>
    /// <param name="value">The original value.</param>
    /// <returns>A logging-safe value.</returns>
    private static string? SanitizeForLog(string? value)
    {
        if (value == null)
        {
            return null;
        }

        // Remove carriage return and line feed characters that can break log structure.
        return value.Replace("\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .SanitizeUntrustedString();
    }

    /// <summary>
    /// Sanitizes log messages derived from user input to prevent log forging by removing line breaks.
    /// </summary>
    /// <param name="messages">The collection of messages to sanitize.</param>
    /// <returns>An enumerable of sanitized messages.</returns>
    private static IEnumerable<string> SanitizeLogMessages(IEnumerable<string> messages)
    {
        if (messages == null)
        {
            yield break;
        }

        foreach (var message in messages)
        {
            if (message == null)
            {
                continue;
            }

            // Replace carriage returns and newlines with spaces to keep each log entry on a single line.
            yield return message
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }

    public async Task<QueryPlanModel> AddAsync(CreateQueryPlanModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "CreateQueryPlanModel cannot be null.");
        }

        // Perform comprehensive validation
        var validationResult = _validator.ValidateQueryPlan(model.InitialQueries, model.SupplementalQueries);

        var safeFacilityId = SanitizeForLog(model.FacilityId);

        if (!validationResult.IsValid)
        {
            _logger.LogError("Query Plan validation failed for facility {FacilityId}: {Errors}",
                safeFacilityId,
                string.Join("; ", SanitizeLogMessages(validationResult.Errors)));

            throw new BadRequestException($"Query Plan validation failed: {validationResult.GetErrorMessage()}");
        }

        // Log warnings if any exist
        if (validationResult.Warnings.Any())
        {
            _logger.LogWarning("Query Plan validation warnings for facility {FacilityId}: {Warnings}",
                safeFacilityId,
                string.Join("; ", SanitizeLogMessages(validationResult.Warnings)));
        }

        var date = DateTime.UtcNow;

        var entity = new QueryPlan
        {
            PlanName = model.PlanName,
            FacilityId = model.FacilityId,
            EHRDescription = model.EHRDescription,
            LookBack = model.LookBack,
            InitialQueries = model.InitialQueries,
            SupplementalQueries = model.SupplementalQueries,
            Type = model.Type,
            CreateDate = date,
            ModifyDate = date
        };

        entity = await _database.QueryPlanRepository.AddAsync(entity);
        await _database.QueryPlanRepository.SaveChangesAsync();

        _logger.LogInformation("Successfully created Query Plan for facility {FacilityId} with type {Type}",
            SanitizeForLog(model.FacilityId),
            model.Type);

        return QueryPlanModel.FromDomain(entity);
    }

    public async Task<QueryPlanModel> UpdateAsync(UpdateQueryPlanModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "UpdateQueryPlanModel cannot be null.");
        }

        // Perform comprehensive validation
        var validationResult = _validator.ValidateQueryPlan(model.InitialQueries, model.SupplementalQueries);

        if (!validationResult.IsValid)
        {
            _logger.LogError("Query Plan validation failed for facility {FacilityId}: {Errors}",
                SanitizeForLog(model.FacilityId),
                string.Join("; ", SanitizeLogMessages(validationResult.Errors)));

            throw new BadRequestException($"Query Plan validation failed: {validationResult.GetErrorMessage()}");
        }

        // Log warnings if any exist
        if (validationResult.Warnings.Any())
        {
            _logger.LogWarning("Query Plan validation warnings for facility {FacilityId}: {Warnings}",
                SanitizeForLog(model.FacilityId),
                string.Join("; ", SanitizeLogMessages(validationResult.Warnings)));
        }

        var existingQueryPlan = await _database.QueryPlanRepository.FirstOrDefaultAsync(
            q => q.FacilityId == model.FacilityId && q.Type == model.Type);

        if (existingQueryPlan == null)
        {
            throw new NotFoundException($"No Query Plan for FacilityId {model.FacilityId} and Type {model.Type} was found.");
        }

        existingQueryPlan.InitialQueries = model.InitialQueries;
        existingQueryPlan.SupplementalQueries = model.SupplementalQueries;
        existingQueryPlan.PlanName = model.PlanName;
        existingQueryPlan.EHRDescription = model.EHRDescription;
        existingQueryPlan.LookBack = model.LookBack;
        existingQueryPlan.ModifyDate = DateTime.UtcNow;

        await _database.QueryPlanRepository.SaveChangesAsync();

        _logger.LogInformation("Successfully updated Query Plan for facility {FacilityId} with type {Type}",
            SanitizeForLog(model.FacilityId),
            model.Type);

        return QueryPlanModel.FromDomain(existingQueryPlan);
    }

    public async Task DeleteAsync(string facilityId, Frequency type, CancellationToken cancellationToken = default)
    {
        var entity = await _database.QueryPlanRepository.SingleOrDefaultAsync(
            q => q.FacilityId == facilityId && q.Type == type);

        if (entity == null)
        {
            throw new NotFoundException($"No Query Plan for FacilityId {facilityId} and Type {type} was found.");
        }

        _database.QueryPlanRepository.Remove(entity);
        await _database.QueryPlanRepository.SaveChangesAsync();

        _logger.LogInformation("Successfully deleted Query Plan for facility {FacilityId} with type {Type}",
            SanitizeForLog(facilityId),
            type);
    }

    public async Task DeleteAllQueryPlansAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var allPlans = await _database.QueryPlanRepository.GetAllAsync(cancellationToken);
        var facilityPlans = allPlans.Where(q => q.FacilityId == facilityId).ToList();

        foreach (var plan in facilityPlans)
        {
            _database.QueryPlanRepository.Remove(plan);
        }

        if (facilityPlans.Any())
        {
            await _database.QueryPlanRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully deleted {Count} Query Plans for facility {FacilityId}",
                facilityPlans.Count,
                SanitizeForLog(facilityId));
        }
        else
        {
            _logger.LogInformation("No Query Plans found to delete for facility {FacilityId}", SanitizeForLog(facilityId));
        }
    }
}