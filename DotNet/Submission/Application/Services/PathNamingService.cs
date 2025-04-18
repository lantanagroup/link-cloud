using Hl7.FhirPath.Sprache;
using LantanaGroup.Link.Submission.Application.Config;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Submission.Application.Services;

public class PathNamingService(IOptions<SubmissionServiceConfig> config, ILogger<PathNamingService> logger)
{
    private const string DirectoryDateFormat = "yyyyMMdd";

    public string GetMeasureShortName(string measure)
    {
        if (config.Value?.MeasureNames == null)
        {
            logger.LogError("Submission service configuration does not contain measure names.");
            throw new Exception("Submission service configuration does not contain measure names.");
        }
        
        // If a URL, may contain |0.1.2 representing the version at the end of the URL
        // Remove it so that we're looking at the generic URL, not the URL specific to a measure version
        string measureWithoutVersion = measure.Contains("|") ?
            measure.Substring(0, measure.LastIndexOf("|", System.StringComparison.Ordinal)) :
            measure;

        var urlShortName = config.Value.MeasureNames
            .FirstOrDefault(x => x.Url == measureWithoutVersion || x.MeasureId == measureWithoutVersion)?
            .ShortName;

        if (!string.IsNullOrWhiteSpace(urlShortName))
            return urlShortName;
        else
            logger.LogError("Submission service configuration does not contain a short name for measure: " + measure);

        return $"{measure.GetHashCode():X}";
    }

    public string GetMeasuresShortName(IEnumerable<string> measures)
    {
        return measures
            .Select(GetMeasureShortName)
            .Aggregate((a, b) => $"{a}+{b}");
    }

    /**
     * Gets the name of the directory for a given submission/report
     * Format: {nhsn-org-id}-{plus-separated-list-of-measure-ids}-{period-start}-{period-end?}-{timestamp}
     */
    public string GetSubmissionDirectoryName(string facilityId, IEnumerable<string> measures, DateTime startDate,
        DateTime endDate, string reportId)
    {
        //Per 2153, don't build with the trailing timestamp
        string measureShortNames = GetMeasuresShortName(measures);
        return $"{facilityId}-{measureShortNames}-{startDate.ToString(DirectoryDateFormat)}-{endDate.ToString(DirectoryDateFormat)}_{reportId}";
    }
}