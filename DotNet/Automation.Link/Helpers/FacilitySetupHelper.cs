using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Net;

namespace LantanaGroup.Link.Automation.Link.Helpers;

public static class FacilitySetupHelper
{
    /// <summary>
    /// The timezone every automation facility is created in. The DMRP reporting period is read in it,
    /// so the two cannot be allowed to drift apart.
    /// </summary>
    private const string FacilityTimeZone = "America/Chicago";

    public static async Task EnsureFacilityAsync(
        IFacilityServiceClient facilityClient,
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFacilityAsync(facilityClient, dmrpClient, output, facilityId,
            measureId != null ? [measureId] : [], cancellationToken);
    }

    /// <summary>
    /// Creates the facility the run reports for, scheduled to report <paramref name="measureIds"/>
    /// monthly.
    /// </summary>
    /// <remarks>
    /// How that schedule gets set depends on whether the Tenant service is hosting the DMRP module.
    /// With DMRP off it is posted with the facility. With DMRP on it is not the caller's to give —
    /// Tenant derives it from the facility's DMRP reporting plans and refuses a request that carries
    /// one — so the same schedule has to be reached by enrolling the facility in those measures and
    /// letting Tenant derive it. Both paths leave the same monthly schedule behind, which is what the
    /// rest of the run and the tenant database validator expect.
    /// </remarks>
    public static async Task EnsureFacilityAsync(
        IFacilityServiceClient facilityClient,
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        string facilityId,
        List<string> measureIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await facilityClient.GetAsync(facilityId, cancellationToken);
        if (existing.IsSuccessStatusCode && existing.Body != null)
        {
            output.WriteLine($"Facility '{facilityId}' already exists. Skipping create.");
            await WaitForFacilityReadConsistencyAsync(facilityClient, output, facilityId, cancellationToken);
            return;
        }

        var dmrpEnabled = await DmrpIsEnabledAsync(dmrpClient, output, cancellationToken);

        var createResponse = await facilityClient.CreateAsync(new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = FacilityTimeZone,
            Vendor = new VendorModel
            {
                Name = "Epic"
            },
            // Empty under DMRP, and not merely unselected: a request that names any report is refused
            // outright. The measures are enrolled below instead.
            ScheduledReports = MonthlySchedule(dmrpEnabled ? [] : measureIds)
        }, cancellationToken);

        if (!createResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to create facility '{facilityId}'. HTTP {createResponse.StatusCode}: {createResponse.RawBody ?? "(no body)"}");
        }

        await WaitForFacilityReadConsistencyAsync(facilityClient, output, facilityId, cancellationToken);

        if (dmrpEnabled)
        {
            await EnrollFacilityInDmrpMeasuresAsync(facilityClient, dmrpClient, output, facilityId,
                measureIds, cancellationToken);
        }
    }

    private static TenantScheduledReportConfig MonthlySchedule(IReadOnlyList<string> measureIds) => new()
    {
        Monthly = measureIds.ToArray(),
        Daily = [],
        Weekly = []
    };

    /// <summary>
    /// Whether the Tenant service is hosting the DMRP module.
    /// </summary>
    /// <remarks>
    /// Asked rather than configured. A disabled module strips its own controllers from the host, so
    /// its routes answer 404 and there is nothing else to read — which keeps this from becoming a
    /// third place the flag has to be set and kept in step with the stack and the Admin UI.
    /// </remarks>
    private static async Task<bool> DmrpIsEnabledAsync(
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        CancellationToken cancellationToken)
    {
        var probe = await dmrpClient.SearchFacilityReportingPlansAsync(pageSize: 1, pageNumber: 1,
            cancellationToken: cancellationToken);

        if (probe.StatusCode == (int)HttpStatusCode.NotFound)
        {
            output.WriteLine("DMRP is not enabled on the Tenant service; the facility's schedule is posted with it.");
            return false;
        }

        if (!probe.IsSuccessStatusCode)
        {
            // Neither answer. Guessing either way strands the run — at facility create if DMRP is on,
            // reporting nothing if it is off — so name the request that failed instead.
            throw new InvalidOperationException(
                "Could not determine whether DMRP is enabled on the Tenant service. " +
                $"GET api/dmrp/reporting-plans returned HTTP {probe.StatusCode}: {probe.RawBody ?? "(no body)"}");
        }

        output.WriteLine("DMRP is enabled on the Tenant service; the facility's schedule is derived from its reporting plans.");
        return true;
    }

    /// <summary>
    /// Enrolls the facility in each measure and has Tenant derive its schedule from that enrollment.
    /// </summary>
    /// <remarks>
    /// The order is forced from both ends: the schedule is derived when the facility is saved, so the
    /// reporting plans have to exist first, but a reporting plan is refused for a facility that does
    /// not exist yet. Neither can go first, so the facility is created with an empty schedule and
    /// saved a second time once there is something to derive one from.
    /// </remarks>
    private static async Task EnrollFacilityInDmrpMeasuresAsync(
        IFacilityServiceClient facilityClient,
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        string facilityId,
        List<string> measureIds,
        CancellationToken cancellationToken)
    {
        if (measureIds.Count == 0)
        {
            output.WriteLine($"No measures to enroll facility '{facilityId}' in; it is scheduled for no reports.");
            return;
        }

        foreach (var measureId in measureIds)
        {
            var mappingId = await EnsureMeasureMappingAsync(dmrpClient, output, measureId, cancellationToken);

            foreach (var (month, year) in ReportingPeriods())
            {
                await EnsureReportingPlanAsync(dmrpClient, output, facilityId, mappingId, month, year,
                    cancellationToken);
            }
        }

        var updated = await facilityClient.UpdateAsync(facilityId, new FacilityModel
        {
            FacilityId = facilityId,
            FacilityName = facilityId,
            TimeZone = FacilityTimeZone,
            Vendor = new VendorModel
            {
                Name = "Epic"
            },
            ScheduledReports = MonthlySchedule([])
        }, cancellationToken);

        if (!updated.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to derive the DMRP schedule for facility '{facilityId}'. HTTP {updated.StatusCode}: {updated.RawBody ?? "(no body)"}");
        }

        output.WriteLine(
            $"Enrolled facility '{facilityId}' in {measureIds.Count} DMRP measure(s); its schedule is derived from its reporting plans.");
    }

    /// <summary>
    /// The reporting periods to enroll the facility for: the one it is in, and the one after it.
    /// </summary>
    /// <remarks>
    /// Enrollment is recorded per period, and Tenant derives the schedule for whichever period the
    /// facility is in when it is saved. A run that crosses midnight on the first of a month between
    /// the two saves would otherwise derive an empty schedule from a period nothing was enrolled for,
    /// then fail much later with an error about scheduled reports rather than about the clock.
    /// </remarks>
    private static IReadOnlyList<(int Month, int Year)> ReportingPeriods()
    {
        var now = FacilityLocalNow();
        var next = now.AddMonths(1);

        return [(now.Month, now.Year), (next.Month, next.Year)];
    }

    private static DateTimeOffset FacilityLocalNow()
    {
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(FacilityTimeZone);
            return TimeZoneInfo.ConvertTime(utcNow, timeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Tenant falls back to UTC for a timezone it cannot read, so match it rather than enroll
            // the facility for a period its schedule will never be derived from.
            return utcNow;
        }
    }

    private static async Task<string> EnsureMeasureMappingAsync(
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        string measureId,
        CancellationToken cancellationToken)
    {
        var existingId = await FindMeasureMappingAsync(dmrpClient, measureId, cancellationToken);
        if (existingId != null)
        {
            output.WriteLine($"DMRP measure mapping for '{measureId}' already exists. Reusing it.");
            return existingId;
        }

        // Measure and dQM are deliberately the same value. The run drives the pipeline with the
        // measure's own id, so that is what the derived schedule has to name for the schedule and the
        // report types to agree. Monthly matches the frequency the non-DMRP path posts.
        var created = await dmrpClient.CreateMeasureMappingAsync(new MeasureMappingModel
        {
            Measure = measureId,
            DQM = measureId,
            Frequency = Frequency.Monthly
        }, cancellationToken);

        if (created.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(created.Body?.Id))
        {
            return created.Body!.Id!;
        }

        // Mappings are shared by every run against a stack, so a run starting alongside another can
        // lose the race to create one. Losing it is not a failure — the mapping it needed now exists.
        if (created.StatusCode == (int)HttpStatusCode.BadRequest)
        {
            existingId = await FindMeasureMappingAsync(dmrpClient, measureId, cancellationToken);
            if (existingId != null)
            {
                output.WriteLine($"DMRP measure mapping for '{measureId}' was created concurrently. Reusing it.");
                return existingId;
            }
        }

        throw new InvalidOperationException(
            $"Failed to create DMRP measure mapping for measure '{measureId}'. HTTP {created.StatusCode}: {created.RawBody ?? "(no body)"}");
    }

    private static async Task<string?> FindMeasureMappingAsync(
        IDmrpServiceClient dmrpClient,
        string measureId,
        CancellationToken cancellationToken)
    {
        // Both filters match exactly, and measure and dQM together are unique, so this is at most one
        // row. Nothing matching answers 204 with no body rather than an empty page.
        var search = await dmrpClient.SearchMeasureMappingsAsync(measure: measureId, dqm: measureId,
            pageSize: 1, pageNumber: 1, cancellationToken: cancellationToken);

        return search.Body?.Records?.FirstOrDefault()?.Id;
    }

    private static async Task EnsureReportingPlanAsync(
        IDmrpServiceClient dmrpClient,
        IAutomationOutput output,
        string facilityId,
        string measureMappingId,
        int month,
        int year,
        CancellationToken cancellationToken)
    {
        var created = await dmrpClient.CreateFacilityReportingPlanAsync(new FacilityReportingPlanRequest
        {
            FacilityId = facilityId,
            MeasureMappingId = measureMappingId,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = true
        }, cancellationToken);

        if (created.IsSuccessStatusCode)
        {
            return;
        }

        if (created.StatusCode == (int)HttpStatusCode.Conflict)
        {
            output.WriteLine($"DMRP reporting plan for facility '{facilityId}' and {month}/{year} already exists. Skipping create.");
            return;
        }

        throw new InvalidOperationException(
            $"Failed to create DMRP reporting plan for facility '{facilityId}' ({month}/{year}). HTTP {created.StatusCode}: {created.RawBody ?? "(no body)"}");
    }

    private static async Task WaitForFacilityReadConsistencyAsync(
        IFacilityServiceClient facilityClient,
        IAutomationOutput output,
        string facilityId,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(20);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            var facility = await facilityClient.GetAsync(facilityId, cancellationToken);
            if (facility.IsSuccessStatusCode && facility.Body != null)
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new InvalidOperationException(
            $"Facility '{facilityId}' was created but could not be read back from Tenant service within {timeout.TotalSeconds:F0}s.");
    }

    public static async Task EnsureNormalizationConfigAsync(
        INormalizationServiceClient normalizationClient,
        IAutomationOutput output,
        string facilityId)
    {
        try
        {
            var response = await normalizationClient.SearchFacilityOperationsAsync(facilityId);
            if (response.IsSuccessStatusCode && response.Body?.Records?.Count > 0)
            {
                output.WriteLine($"Normalization config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            var normResp = await normalizationClient.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
            {
                ResourceTypes = ["Location"],
                FacilityId = facilityId,
                Operation = new CreateNormalizationOperationDetailsApiModel
                {
                    OperationType = "CopyProperty",
                    Name = "Copy Location Identifier to Type",
                    Description = "A Test Operation",
                    SourceFhirPath = "identifier.value",
                    TargetFhirPath = "type[0].coding.code"
                },
                Description = "Copy Location Identifier to Code",
                VendorVersionIds = []
            });

            if (!normResp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Failed to create normalization operation for facility '{facilityId}'. HTTP {normResp.StatusCode}: {normResp.RawBody ?? "(no body)"}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"CreateOperationAsync failed for facility '{facilityId.SanitizeForLog()}': {ex.GetType().Name}");
            throw;
        }
    }

    public static async Task EnsureQueryPlansAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        List<string> measureIds,
        string ehrDescription,
        QueryPlanInput? externalQueryPlan = null)
    {
        // Query plans are keyed by (facilityId, type) — not per measure.
        // Create each plan type once using the first measure as the plan name.
        //
        // The type MUST match the ReportableEvent-to-Frequency mapping the DataAcquisition
        // service uses to select a plan (ReportableEventToQueryPlanTypeFactory):
        //   - Discharge: the QueryDispatch discharge path sends ReportableEvent=Discharge for
        //     patients discharged inside the report window (Frequency.Discharge).
        //   - Daily/Monthly: the End-of-Report-Period job (Report.DataAcquisitionRequestedProducer)
        //     derives the event from schedule.Frequency (EOD/EOM). Keep both plans present so
        //     scheduled scenarios can run against either ACH Daily or ACH Monthly measures
        //     without configuration-missing failures.
        var planName = measureIds.Count > 0 ? measureIds[0] : null;
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, planName, ehrDescription, "Discharge", externalQueryPlan);
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, planName, ehrDescription, "Daily", externalQueryPlan);
        await EnsureQueryPlanAsync(dataAcqClient, output, facilityId, planName, ehrDescription, "Monthly", externalQueryPlan);
    }

    public static async Task EnsureQueryConfigAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        AutomationConfig config,
        IAutomationOutput output,
        string facilityId)
    {
        // Keep concurrency high enough to avoid single-request bottlenecks,
        // but cap it to reduce downstream service saturation in large volume runs.
        var effectiveMaxConcurrentRequests = Math.Clamp(config.FhirQuery.MaxConcurrentRequests, 1, 8);

        var created = await dataAcqClient.CreateFhirQueryConfigurationAsync(new CreateFhirQueryConfigurationRequestApiModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = config.FacilityFhirServerBase,
            MaxConcurrentRequests = effectiveMaxConcurrentRequests,
            MaxRetries = 3,
            MinAcquisitionPullTime = config.FhirQuery.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = config.FhirQuery.MaxAcquisitionPullTime,
            TimeZone = config.FhirQuery.TimeZone
        });

        if (!created.IsSuccessStatusCode)
        {
            if (created.StatusCode == (int)HttpStatusCode.Conflict)
            {
                output.WriteLine($"Query config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            throw new InvalidOperationException(
                $"Failed to create query config for facility '{facilityId}'. HTTP {created.StatusCode}: {created.RawBody ?? "(no body)"}");
        }
    }

    public static async Task EnsureQueryDispatchConfigAsync(
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId)
    {
        var qdResp = await queryDispatchClient.UpsertQueryDispatchConfigurationAsync(
            facilityId,
            new QueryDispatchConfigurationApiModel
            {
                FacilityId = facilityId,
                DispatchSchedules =
                [
                    new DispatchScheduleApiModel
                    {
                        Event = "Discharge",
                        Duration = "PT0S"
                    }
                ]
            });

        if (!qdResp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to upsert query dispatch config for facility '{facilityId}'. HTTP {qdResp.StatusCode}: {qdResp.RawBody ?? "(no body)"}");

        output.WriteLine($"Ensured query dispatch config for facility '{facilityId}'.");
    }

    public static async Task EnsureCensusConfigAsync(
        ICensusServiceClient censusClient,
        IAutomationOutput output,
        string facilityId,
        string scheduledTrigger = "0 0/5 * * * ?",
        bool enabled = true)
    {
        var existing = await censusClient.GetCensusConfigAsync(facilityId);
        if (existing.IsSuccessStatusCode && existing.Body != null)
        {
            if (string.Equals(existing.Body.ScheduledTrigger, scheduledTrigger, StringComparison.Ordinal)
                && existing.Body.Enabled == enabled)
            {
                output.WriteLine($"Census config for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            var updateResp = await censusClient.UpdateCensusConfigAsync(facilityId, new CensusConfigApiModel
            {
                FacilityId = facilityId,
                ScheduledTrigger = scheduledTrigger,
                Enabled = enabled
            });

            if (!updateResp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Failed to update census config for facility '{facilityId}'. HTTP {updateResp.StatusCode}: {updateResp.RawBody ?? "(no body)"}");

            output.WriteLine($"Updated census config for facility '{facilityId}'.");
            return;
        }

        var createResp = await censusClient.CreateCensusConfigAsync(new CensusConfigApiModel
        {
            FacilityId = facilityId,
            ScheduledTrigger = scheduledTrigger,
            Enabled = enabled
        });

        if (!createResp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to create census config for facility '{facilityId}'. HTTP {createResp.StatusCode}: {createResp.RawBody ?? "(no body)"}");

        output.WriteLine($"Created census config for facility '{facilityId}'.");
    }

    public static async Task EnsureFhirListConfigAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        AutomationConfig config,
        IAutomationOutput output,
        string facilityId)
    {
        var existing = await dataAcqClient.GetFhirListConfigurationAsync(facilityId);
        if (existing.IsSuccessStatusCode)
        {
            output.WriteLine($"FHIR list config for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        // Create all 6 required list combinations (Admit/Discharge x 3 timeframes)
        var listConfigs = new[]
        {
            new { Status = "Admit", TimeFrame = "LessThan24Hours", FhirId = $"census-{facilityId}-admit-lt24" },
            new { Status = "Admit", TimeFrame = "Between24To48Hours", FhirId = $"census-{facilityId}-admit-24to48" },
            new { Status = "Admit", TimeFrame = "MoreThan48Hours", FhirId = $"census-{facilityId}-admit-gt48" },
            new { Status = "Discharge", TimeFrame = "LessThan24Hours", FhirId = $"census-{facilityId}-discharge-lt24" },
            new { Status = "Discharge", TimeFrame = "Between24To48Hours", FhirId = $"census-{facilityId}-discharge-24to48" },
            new { Status = "Discharge", TimeFrame = "MoreThan48Hours", FhirId = $"census-{facilityId}-discharge-gt48" }
        };

        var request = new
        {
            FacilityId = facilityId,
            FhirBaseServerUrl = config.FacilityFhirServerBase,
            EHRPatientLists = listConfigs.Select(c => new
            {
                c.Status,
                c.TimeFrame,
                c.FhirId
            }).ToList()
        };

        var createResp = await dataAcqClient.CreateFhirListConfigurationAsync(request);
        if (!createResp.IsSuccessStatusCode)
        {
            if (createResp.StatusCode == (int)HttpStatusCode.Conflict)
            {
                output.WriteLine($"FHIR list config for facility '{facilityId}' already exists (conflict). Continuing.");
                return;
            }

            throw new InvalidOperationException(
                $"Failed to create FHIR list config for facility '{facilityId}'. HTTP {createResp.StatusCode}: {createResp.RawBody ?? "(no body)"}");
        }

        output.WriteLine($"Created FHIR list config for facility '{facilityId}'.");
    }

    public static async Task CleanupQueryDispatchConfigAsync(
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId)
    {
        await queryDispatchClient.DeleteQueryDispatchConfigurationAsync(facilityId);
        output.WriteLine($"Query dispatch config cleanup complete for facility '{facilityId}'.");
    }

    public static async Task CleanupFacilityAsync(
        IFacilityServiceClient facilityClient,
        INormalizationServiceClient normalizationClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId)
    {
        await queryDispatchClient.DeleteQueryDispatchConfigurationAsync(facilityId);
        await normalizationClient.DeleteFacilityOperationsAsync(facilityId);
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Discharge");
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Daily");
        await dataAcqClient.DeleteQueryPlanAsync(facilityId, "Monthly");
        await dataAcqClient.DeleteFhirQueryConfigurationAsync(facilityId);
        await facilityClient.DeleteAsync(facilityId);
        output.WriteLine("Facility cleanup complete.");
    }

    public static async Task SoftDeleteRunDataAsync(
        IReportServiceClient reportClient,
        IDataAcquisitionServiceClient dataAcqClient,
        IQueryDispatchServiceClient queryDispatchClient,
        IAutomationOutput output,
        string facilityId,
        string reportId)
    {
        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(reportId))
            return;

        await reportClient.SoftDeleteScheduleAsync(reportId);
        await dataAcqClient.SoftDeleteLogsByFacilityAsync(facilityId);
        await queryDispatchClient.DeleteQueryDispatchConfigurationAsync(facilityId);

        output.WriteLine($"Soft-deleted report '{reportId}', DA logs, and query dispatch config for facility '{facilityId}'.");
    }

    private static async Task EnsureQueryPlanAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        string? measureId,
        string ehrDescription,
        string type,
        QueryPlanInput? externalQueryPlan = null)
    {
        var jBody = QueryPlanBuilder.BuildQueryPlan(facilityId, measureId, ehrDescription, type, externalQueryPlan);
        var body = new CreateQueryPlanRequestApiModel
        {
            PlanName = jBody.Value<string>("PlanName"),
            FacilityId = jBody.Value<string>("FacilityId") ?? facilityId,
            EHRDescription = jBody.Value<string>("EHRDescription") ?? ehrDescription,
            LookBack = jBody.Value<string>("LookBack") ?? "P0D",
            Type = jBody.Value<string>("Type") ?? type,
            InitialQueries = jBody["InitialQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
            SupplementalQueries = jBody["SupplementalQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>()
        };

        var createdPlan = await dataAcqClient.CreateQueryPlanAsync(facilityId, body);
        if (!createdPlan.IsSuccessStatusCode)
        {
            if (createdPlan.StatusCode == (int)HttpStatusCode.Conflict)
            {
                output.WriteLine($"{type} query plan for facility '{facilityId}' already exists. Skipping create.");
                return;
            }

            throw new InvalidOperationException(
                $"Failed to create {type} query plan for facility '{facilityId}'. HTTP {createdPlan.StatusCode}: {createdPlan.RawBody ?? "(no body)"}");
        }
    }
}