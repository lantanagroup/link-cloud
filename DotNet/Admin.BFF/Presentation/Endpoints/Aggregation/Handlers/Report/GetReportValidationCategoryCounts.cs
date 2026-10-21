using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Validation;
using LantanaGroup.Link.Shared.Application.Models.Report;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Net;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;

/// <summary>
/// Serves a report's stored validation counts per category, with each category's current guidance and severity.
/// </summary>
public static class GetReportValidationCategoryCounts
{
    /// <summary>
    /// Looks up the report schedule's facility, reads the counts from validation, and adds category details.
    /// </summary>
    public static async Task<IResult> Handle(
        ILoggerFactory loggerFactory,
        HttpContext context,
        ReportService reportService,
        ValidationService validationService,
        string reportScheduleId,
        bool? submit = null)
    {
        var logger = loggerFactory.CreateLogger("GetReportValidationCategoryCounts");

        if (string.IsNullOrWhiteSpace(reportScheduleId) || !Guid.TryParse(reportScheduleId, out _))
        {
            return Results.BadRequest("Invalid Id format");
        }

        try
        {
            var scheduleResponse = await reportService.GetReportScheduleById(
                context.User,
                context.RequestAborted,
                reportScheduleId);
            if (!scheduleResponse.IsSuccessStatusCode)
            {
                return UpstreamFailure(scheduleResponse.StatusCode, $"Report schedule '{reportScheduleId}' not found.");
            }

            var schedule = await scheduleResponse.Content.ReadFromJsonAsync<ScheduledReportListSummary>(
                cancellationToken: context.RequestAborted);
            if (schedule is null || string.IsNullOrWhiteSpace(schedule.FacilityId))
            {
                logger.LogError("Report schedule {ReportScheduleId} has no facility",
                    reportScheduleId.SanitizeForLog());
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
            }

            var countsResponse = await validationService.GetReportCategoryCounts(
                context.User,
                schedule.FacilityId,
                reportScheduleId,
                submit,
                context.RequestAborted);
            if (!countsResponse.IsSuccessStatusCode)
            {
                return UpstreamFailure(
                    countsResponse.StatusCode,
                    $"Validation counts are unavailable for report schedule '{reportScheduleId}'.");
            }

            var counts = await countsResponse.Content.ReadFromJsonAsync<ValidationReportCategoryCountsResponse>(
                cancellationToken: context.RequestAborted);
            if (counts is null)
            {
                logger.LogError("Validation returned no counts body for report {ReportScheduleId}",
                    reportScheduleId.SanitizeForLog());
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
            }

            await AddCategoryDetailsAsync(logger, context, validationService, counts.Categories, reportScheduleId);

            return Results.Ok(new ReportValidationCategoryCounts
            {
                ReportScheduleId = reportScheduleId,
                FacilityId = schedule.FacilityId,
                LastUpdated = counts.LastUpdated,
                Categories = counts.Categories
            });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception getting validation category counts for report {ReportScheduleId}",
                reportScheduleId.SanitizeForLog());
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static IResult UpstreamFailure(HttpStatusCode statusCode, string notFoundDetail)
    {
        return statusCode switch
        {
            HttpStatusCode.NotFound => Results.Problem(notFoundDetail, statusCode: StatusCodes.Status404NotFound),
            HttpStatusCode.Unauthorized => Results.Unauthorized(),
            HttpStatusCode.Forbidden => Results.Forbid(),
            _ => Results.Problem(statusCode: (int)statusCode)
        };
    }

    /// <summary>
    /// The counts are what the caller needs, so a failure to read the category list leaves guidance and severity
    /// unset rather than failing the request.
    /// </summary>
    private static async Task AddCategoryDetailsAsync(ILogger logger,
                                                      HttpContext context,
                                                      ValidationService validationService,
                                                      List<ValidationCategoryCount> counts,
                                                      string reportScheduleId)
    {
        if (counts.Count == 0)
        {
            return;
        }

        var categoriesResponse = await validationService.GetCategories(context.User, context.RequestAborted);
        if (!categoriesResponse.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Validation categories returned {StatusCode}; counts for report {ReportScheduleId} have no guidance",
                (int)categoriesResponse.StatusCode,
                reportScheduleId.SanitizeForLog());
            return;
        }

        var categories = await categoriesResponse.Content.ReadFromJsonAsync<List<ValidationCategory>>(
            cancellationToken: context.RequestAborted) ?? [];
        // Validation compares category ids case-insensitively, so match them the same way.
        var categoriesById = categories
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var count in counts)
        {
            if (!categoriesById.TryGetValue(count.CategoryId, out var category))
            {
                continue;
            }

            count.Severity = category.Severity;
            count.Guidance = category.Guidance;
        }
    }
}
