using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Validators;

/// <summary>
/// Enforces the consistency rule between a facility's parent-organization location
/// resolution (<see cref="Infrastructure.Entities.OrganizationLocationConfiguration"/>.IsActive)
/// and its query plans: while location resolution is active, the initial queries of every
/// frequency plan must include both an Encounter and a Location query.
/// </summary>
public interface ILocationResolutionValidator
{
    /// <summary>
    /// Validates that every frequency plan for the facility has both an Encounter and a Location
    /// query in its initial queries. Called when a parent-organization configuration is being
    /// activated. Throws <see cref="BadRequestException"/> when one or more plans are non-compliant.
    /// </summary>
    Task ValidateActivationAsync(string facilityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the supplied initial queries include both an Encounter and a Location query
    /// when location resolution is active for the facility. Called when a query plan is created or
    /// updated. A no-op when no active configuration exists. Throws <see cref="BadRequestException"/>
    /// when the plan would leave the facility in an invalid state for location resolution.
    /// </summary>
    Task ValidateQueryPlanSaveAsync(
        string facilityId,
        Frequency type,
        IReadOnlyDictionary<string, IQueryConfig>? initialQueries,
        CancellationToken cancellationToken = default);
}

public class LocationResolutionValidator : ILocationResolutionValidator
{
    private const string EncounterResource = "Encounter";
    private const string LocationResource = "Location";

    private readonly IQueryPlanQueries _queryPlanQueries;
    private readonly IOrganizationLocationConfigurationQueries _locationConfigurationQueries;

    public LocationResolutionValidator(
        IQueryPlanQueries queryPlanQueries,
        IOrganizationLocationConfigurationQueries locationConfigurationQueries)
    {
        _queryPlanQueries = queryPlanQueries ?? throw new ArgumentNullException(nameof(queryPlanQueries));
        _locationConfigurationQueries = locationConfigurationQueries ?? throw new ArgumentNullException(nameof(locationConfigurationQueries));
    }

    public async Task ValidateActivationAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var plans = await _queryPlanQueries.FindAsync(q => q.FacilityId == facilityId, cancellationToken);

        var offenders = new List<(Frequency Type, bool MissingEncounter, bool MissingLocation)>();
        foreach (var plan in plans)
        {
            var (hasEncounter, hasLocation) = InspectInitialQueries(plan.InitialQueries);
            if (!hasEncounter || !hasLocation)
            {
                offenders.Add((plan.Type, !hasEncounter, !hasLocation));
            }
        }

        if (offenders.Count == 0)
        {
            return;
        }

        throw new BadRequestException(BuildActivationErrorMessage(offenders));
    }

    public async Task ValidateQueryPlanSaveAsync(
        string facilityId,
        Frequency type,
        IReadOnlyDictionary<string, IQueryConfig>? initialQueries,
        CancellationToken cancellationToken = default)
    {
        var isActive = await _locationConfigurationQueries.HasActiveByFacilityIdAsync(facilityId, cancellationToken);
        if (!isActive)
        {
            return;
        }

        var (hasEncounter, hasLocation) = InspectInitialQueries(initialQueries);
        if (hasEncounter && hasLocation)
        {
            return;
        }

        string requirement = (!hasEncounter && !hasLocation)
            ? "both an Encounter and a Location query are required in the initial queries"
            : !hasEncounter
                ? "an Encounter query is required in the initial queries"
                : "a Location query is required in the initial queries";

        throw new BadRequestException(
            $"Cannot save this query plan: {requirement} while location resolution (parent organization) is active.");
    }

    private static string BuildActivationErrorMessage(
        IReadOnlyList<(Frequency Type, bool MissingEncounter, bool MissingLocation)> offenders)
    {
        if (offenders.Count == 1)
        {
            var (type, missingEncounter, missingLocation) = offenders[0];

            string requirement = (missingEncounter && missingLocation)
                ? "must include both an Encounter and a Location query"
                : missingEncounter
                    ? "must include an Encounter query"
                    : "must include a Location query";

            return $"Cannot enable location resolution: the {type} query plan's initial queries {requirement}.";
        }

        var frequencyList = FormatFrequencyList(offenders.Select(o => o.Type.ToString()).ToList());
        return $"Cannot enable location resolution: the {frequencyList} query plans are missing required " +
               "Encounter/Location queries in their initial queries.";
    }

    private static (bool HasEncounter, bool HasLocation) InspectInitialQueries(
        IReadOnlyDictionary<string, IQueryConfig>? initialQueries)
    {
        bool hasEncounter = false;
        bool hasLocation = false;

        if (initialQueries == null)
        {
            return (hasEncounter, hasLocation);
        }

        foreach (var config in initialQueries.Values)
        {
            var resourceType = GetResourceType(config);
            if (string.Equals(resourceType, EncounterResource, StringComparison.OrdinalIgnoreCase))
            {
                hasEncounter = true;
            }
            else if (string.Equals(resourceType, LocationResource, StringComparison.OrdinalIgnoreCase))
            {
                hasLocation = true;
            }
        }

        return (hasEncounter, hasLocation);
    }

    private static string? GetResourceType(IQueryConfig? config) => config switch
    {
        ParameterQueryConfig parameterConfig => parameterConfig.ResourceType,
        ReferenceQueryConfig referenceConfig => referenceConfig.ResourceType,
        _ => null
    };

    private static string FormatFrequencyList(IReadOnlyList<string> frequencies)
    {
        if (frequencies.Count == 1)
        {
            return frequencies[0];
        }

        if (frequencies.Count == 2)
        {
            return $"{frequencies[0]} and {frequencies[1]}";
        }

        var leading = string.Join(", ", frequencies.Take(frequencies.Count - 1));
        return $"{leading}, and {frequencies[^1]}";
    }
}
