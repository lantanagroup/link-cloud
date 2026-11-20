using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Extensions;

/// <summary>
/// Derives the per-type counts that report statistics read from a log's acquired resource ids.
/// </summary>
public static class ResourceIdExtensions
{
    /// <summary>
    /// The text before the first '/' of an id such as "Observation/123", or the whole id when no '/' follows
    /// at least one character.
    /// </summary>
    public static string ToResourceTypeKey(this string resourceId)
    {
        var slash = resourceId.IndexOf('/');

        return slash > 0 ? resourceId[..slash] : resourceId;
    }

    /// <summary>
    /// One count row per resource type in <paramref name="resourceIds"/>, skipping empty ids.
    /// </summary>
    /// <remarks>
    /// Types are grouped case-insensitively, matching the database collation the counts are unique under.
    /// </remarks>
    public static List<DataAcquisitionLogResourceTypeCount> ToResourceTypeCounts(this IEnumerable<string> resourceIds,
                                                                                long dataAcquisitionLogId)
    {
        return resourceIds
            .Where(id => !string.IsNullOrEmpty(id))
            .GroupBy(id => id.ToResourceTypeKey(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new DataAcquisitionLogResourceTypeCount
            {
                DataAcquisitionLogId = dataAcquisitionLogId,
                ResourceType = group.Key,
                Count = group.Count()
            })
            .ToList();
    }
}
