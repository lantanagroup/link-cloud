namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Non-asserting, read-only snapshot of the pipeline's database state.
/// Uses <see cref="PipelineDataReader"/> for all data retrieval.
/// </summary>
public class PipelineSnapshot
{
    public sealed record NormalizationSuiteSnapshot(
        string SuiteName,
        IReadOnlyList<NormalizationSequenceSnapshot> Sequences,
        IReadOnlyList<NormalizationSequenceOperationSnapshot> StandaloneOperations);

    public sealed record NormalizationSequenceSnapshot(
        string SequenceName,
        IReadOnlyList<NormalizationSequenceOperationSnapshot> Operations);

    public sealed record NormalizationSequenceOperationSnapshot(
        int Sequence,
        string OperationType,
        string OperationName,
        IReadOnlyList<string> ResourceTypes);

    private readonly PipelineDataReader _reader;

    public PipelineSnapshot(PipelineDataReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Writes a complete, non-asserting pipeline snapshot to test output.
    /// Safe to call at any point � never throws.
    /// </summary>
    public async Task WriteFullSnapshotAsync(
        IAutomationOutput output,
        string facilityId,
        string reportId,
        NormalizationSuiteSnapshot? normalizationSuiteSnapshot = null)
    {
        output.WriteLine("\n=== PIPELINE DIAGNOSTIC SNAPSHOT ===\n");

        var scheduleId = Guid.Parse(reportId);

        await WriteReportSnapshot(output, facilityId, scheduleId);
        await WriteDataAcquisitionSnapshot(output, facilityId, reportId);
        await WriteOrganizationLocationSnapshot(output, facilityId);
        await WriteNormalizationSnapshot(output, facilityId, normalizationSuiteSnapshot);
        await WriteTenantSnapshot(output, facilityId);
        await WriteValidationSnapshot(output, facilityId, reportId);

        output.WriteLine("\n=== END SNAPSHOT ===\n");
    }

    private async Task WriteReportSnapshot(IAutomationOutput output, string facilityId, Guid scheduleId)
    {
        try
        {
            var schedule = await _reader.GetReportScheduleAsync(scheduleId);
            if (schedule == null)
            {
                output.WriteLine("[Snapshot][ReportSchedule]     NOT FOUND");
            }
            else
            {
                output.WriteLine($"[Snapshot][ReportSchedule]     Status={schedule.Status}, " +
                                 $"Frequency={schedule.Frequency}, AdHocType={schedule.AdHocType}, " +
                                 $"EnableSubmission={schedule.EnableSubmission}");
            }

            var reportTypes = await _reader.GetScheduleReportTypesAsync(scheduleId);
            output.WriteLine($"[Snapshot][ScheduleReportType] {reportTypes.Count} row(s)" +
                             (reportTypes.Count > 0 ? $" | {string.Join(", ", reportTypes.Select(rt => rt.ReportType))}" : ""));

            var entries = await _reader.GetReportEntriesAsync(scheduleId);
            if (entries.Count == 0)
            {
                output.WriteLine("[Snapshot][ReportEntry]         0 rows");
            }
            else
            {
                var byReporting = entries.GroupBy(e => e.ReportingStatus)
                    .Select(g => $"{g.Key}={g.Count()}");
                var bySubmission = entries.Where(e => e.SubmissionStatus != null)
                    .GroupBy(e => e.SubmissionStatus)
                    .Select(g => $"{g.Key}={g.Count()}");

                output.WriteLine($"[Snapshot][ReportEntry]         {entries.Count} row(s) | " +
                                 $"Reporting: {string.Join(", ", byReporting)} | " +
                                 $"Submission: {string.Join(", ", bySubmission)}");

                foreach (var entry in entries.Take(25))
                {
                    var measureStatuses = string.Join(", ", entry.MeasureReports.Select(mr =>
                        $"{mr.ReportType}:{mr.Status}"));
                    output.WriteLine(
                        $"[Snapshot][ReportEntry]         patient={entry.PatientId} reporting={entry.ReportingStatus} submission={entry.SubmissionStatus}" +
                        (string.IsNullOrWhiteSpace(measureStatuses) ? "" : $" measures=[{measureStatuses}]"));
                }

                if (entries.Count > 25)
                    output.WriteLine($"[Snapshot][ReportEntry]         ... {entries.Count - 25} more patient(s)");
            }

            var measureReports = await _reader.GetEntryMeasureReportsAsync(scheduleId);
            if (measureReports.Count == 0)
            {
                output.WriteLine("[Snapshot][EntryMeasureReport]  0 rows");
            }
            else
            {
                var byStatus = measureReports.GroupBy(r => r.Status)
                    .Select(g => $"{g.Key}={g.Count()}");
                var withMrId = measureReports.Count(r => !string.IsNullOrWhiteSpace(r.MeasureReportId));

                output.WriteLine($"[Snapshot][EntryMeasureReport]  {measureReports.Count} row(s) | " +
                                 $"Status: {string.Join(", ", byStatus)} | " +
                                 $"WithMeasureReportId: {withMrId}/{measureReports.Count}");
            }

            var populations = await _reader.GetReportPopulationsAsync(scheduleId, facilityId);
            var groupCount = populations.SelectMany(p => p.GroupPopulations).Count();
            var mrpCount = populations.SelectMany(p => p.GroupPopulations).SelectMany(gp => gp.MeasureReportPopulations).Count();
            output.WriteLine($"[Snapshot][ReportPopulation]    {populations.Count} population(s), " +
                             $"{groupCount} group(s), {mrpCount} measure report population(s)");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Report] Error querying Report DB: {ex.Message}");
        }
    }

    private async Task WriteDataAcquisitionSnapshot(IAutomationOutput output, string facilityId, string reportId)
    {
        try
        {
            var summary = await _reader.GetDataAcquisitionReportSummaryAsync(reportId);
            if (summary == null || summary.TotalLogs == 0)
            {
                output.WriteLine("[Snapshot][DataAcqLog]          0 rows");
            }
            else
            {
                var byStatus = string.Join(", ", summary.StatusCounts.Select(s => $"{s.Status}={s.Count}"));
                output.WriteLine($"[Snapshot][DataAcqLog]          {summary.TotalLogs} row(s) for {summary.TotalPatients} patient(s) | {byStatus}");
                output.WriteLine($"[Snapshot][DataAcqLog]          Resources acquired: {summary.TotalResourcesAcquired}, " +
                                 $"Avg completion: {summary.AverageCompletionTimeMs}ms, Retries: {summary.TotalRetryAttempts}");
            }

            var hasConfig = await _reader.HasFhirQueryConfigurationAsync(facilityId);
            output.WriteLine($"[Snapshot][FhirQueryConfig]     {(hasConfig ? "exists" : "NOT FOUND")}");

            var plans = await _reader.GetQueryPlansAsync(facilityId);
            output.WriteLine($"[Snapshot][QueryPlan]           {plans.Count} plan(s)" +
                             (plans.Count > 0 ? $" | {string.Join(", ", plans.Select(p => p.Type))}" : ""));

            var logs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);
            foreach (var log in logs.Take(40))
            {
                var types = string.Join(",", log.FhirQueries.SelectMany(q => q.ResourceTypes).Distinct());
                var acquired = log.ResourceAcquiredIds.Count;
                output.WriteLine(
                    $"[Snapshot][DataAcqLog]          log={log.Id} patient={log.PatientId} phase={log.QueryPhase} status={log.Status} types=[{types}] acquired={acquired}");
            }

            if (logs.Count > 40)
                output.WriteLine($"[Snapshot][DataAcqLog]          ... {logs.Count - 40} more log(s)");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][DataAcq] Error querying DataAcquisition DB: {ex.Message}");
        }
    }

    private async Task WriteOrganizationLocationSnapshot(IAutomationOutput output, string facilityId)
    {
        try
        {
            var configs = await _reader.GetOrganizationLocationConfigurationsAsync(facilityId);
            var mappings = await _reader.GetOrganizationLocationMappingsAsync(facilityId);
            var encounterMappings = await _reader.GetEncounterMappingsAsync(facilityId);

            var activeConfigs = configs.Count(c => c.IsActive);
            var activeConditions = configs.Where(c => c.IsActive).Sum(c => c.ConditionsCount);
            var activeMappings = mappings.Count(m => m.IsActive);
            var orgMappings = mappings.Count(m => m.IsActive && m.IsOrgLocation);
            var orgEncounters = encounterMappings.Count(m => m.MappedToOrg);

            output.WriteLine(
                $"[Snapshot][OrgLocation]         configs={configs.Count} activeConfigs={activeConfigs} activeConditions={activeConditions} " +
                $"locationMappings={mappings.Count} active={activeMappings} isOrgLocation={orgMappings} " +
                $"encounterMappings={encounterMappings.Count} mappedToOrg={orgEncounters}");

            foreach (var mapping in mappings.Take(20))
            {
                output.WriteLine(
                    $"[Snapshot][OrgLocation]         location={mapping.LocationId} active={mapping.IsActive} isOrgLocation={mapping.IsOrgLocation} partOf={mapping.PartOfValue ?? "-"}");
            }

            if (mappings.Count > 20)
                output.WriteLine($"[Snapshot][OrgLocation]         ... {mappings.Count - 20} more location mapping(s)");

            foreach (var mapping in encounterMappings.Take(20))
            {
                var locationIds = string.Join(",", mapping.EncounterLocations.Select(l => l.LocationId));
                output.WriteLine(
                    $"[Snapshot][EncounterMapping]    patient={mapping.PatientId} encounter={mapping.EncounterId} mappedToOrg={mapping.MappedToOrg} locations=[{locationIds}]");
            }

            if (encounterMappings.Count > 20)
                output.WriteLine($"[Snapshot][EncounterMapping]    ... {encounterMappings.Count - 20} more encounter mapping(s)");

            if (activeConfigs > 0 && encounterMappings.Count > 0 && orgEncounters == 0)
            {
                output.WriteLine(
                    "[Snapshot][OrgLocation]         WARNING: org-location mapping is configured but no encounters are MappedToOrg. " +
                    "Non-org encounters are stripped from the MeasureEval bundle, which yields empty Initial Population and no ABS patient files.");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][OrgLocation] Error querying org-location mapping state: {ex.Message}");
        }
    }

    private async Task WriteNormalizationSnapshot(
        IAutomationOutput output,
        string facilityId,
        NormalizationSuiteSnapshot? normalizationSuiteSnapshot)
    {
        try
        {
            var operations = await _reader.GetOperationsAsync(facilityId);

            if (operations.Count == 0)
            {
                output.WriteLine("[Snapshot][NormOperation]       0 rows");
            }
            else
            {
                var byType = operations.GroupBy(o => o.OperationType)
                    .Select(g => $"{g.Key}={g.Count()}");
                var disabledCount = operations.Count(o => o.IsDisabled);

                output.WriteLine($"[Snapshot][NormOperation]       {operations.Count} operation(s) | " +
                                 $"Types: {string.Join(", ", byType)} | " +
                                 $"Disabled: {disabledCount}");

                foreach (var op in operations)
                {
                    var resourceTypes = op.ResourceTypes;
                    output.WriteLine($"[Snapshot][NormOperation]         Id={op.Id}, Type={op.OperationType}, " +
                                     $"Name={op.Name}, ResourceTypes=[{string.Join(", ", resourceTypes)}]");
                }
            }

            var sequences = await _reader.GetOperationSequencesAsync(facilityId);

            if (normalizationSuiteSnapshot is not null)
            {
                output.WriteLine($"[Snapshot][NormSequence]        {normalizationSuiteSnapshot.Sequences.Count} sequence(s) from suite '{normalizationSuiteSnapshot.SuiteName}'");

                foreach (var sequence in normalizationSuiteSnapshot.Sequences)
                {
                    output.WriteLine($"[Snapshot][NormSequence]          {sequence.SequenceName} ({sequence.Operations.Count} op(s))");

                    foreach (var operation in sequence.Operations.OrderBy(o => o.Sequence))
                    {
                        output.WriteLine($"[Snapshot][NormSequence]            Sequence={operation.Sequence}, OperationType={operation.OperationType}, Name={operation.OperationName}, ResourceTypes=[{string.Join(", ", operation.ResourceTypes)}]");
                    }
                }

                if (normalizationSuiteSnapshot.StandaloneOperations.Count > 0)
                {
                    output.WriteLine($"[Snapshot][NormSequence]          Standalone Operations ({normalizationSuiteSnapshot.StandaloneOperations.Count} op(s))");
                    foreach (var operation in normalizationSuiteSnapshot.StandaloneOperations.OrderBy(o => o.Sequence))
                    {
                        output.WriteLine($"[Snapshot][NormSequence]            Sequence={operation.Sequence}, OperationType={operation.OperationType}, Name={operation.OperationName}, ResourceTypes=[{string.Join(", ", operation.ResourceTypes)}]");
                    }
                }

                output.WriteLine($"[Snapshot][NormSequenceRow]     {sequences.Count} operation-sequence row(s) in Normalization service");
            }
            else if (sequences.Count == 0)
            {
                output.WriteLine("[Snapshot][NormSequence]        0 rows");
            }

            if (sequences.Count > 0)
            {
                output.WriteLine($"[Snapshot][NormRuntimeSequence] {sequences.Count} operation-sequence row(s) in Normalization service (per resource type)");
                foreach (var seq in sequences
                             .OrderBy(s => s.ResourceType, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(s => s.Sequence))
                {
                    output.WriteLine(
                        $"[Snapshot][NormRuntimeSequence]   {seq.ResourceType}#{seq.Sequence} {seq.OperationType} '{seq.OperationName}'");
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Normalization] Error querying Normalization DB: {ex.Message}");
        }
    }

    private async Task WriteTenantSnapshot(IAutomationOutput output, string facilityId)
    {
        try
        {
            var facility = await _reader.GetFacilityAsync(facilityId);

            if (facility == null)
            {
                output.WriteLine("[Snapshot][Tenant]              Facility NOT FOUND");
                return;
            }

            var monthly = facility.ScheduledReports?.Monthly ?? [];
            var daily = facility.ScheduledReports?.Daily ?? [];
            var weekly = facility.ScheduledReports?.Weekly ?? [];

            output.WriteLine($"[Snapshot][Tenant]              FacilityId={facility.FacilityId}, " +
                             $"Name={facility.FacilityName}, TimeZone={facility.TimeZone}, " +
                             $"IsDeleted={facility.IsDeleted}, Created={facility.CreateDate:O}");
            output.WriteLine($"[Snapshot][Tenant]              ScheduledReports: " +
                             $"Monthly=[{string.Join(", ", monthly)}], " +
                             $"Daily=[{string.Join(", ", daily)}], " +
                             $"Weekly=[{string.Join(", ", weekly)}]");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Tenant] Error querying Tenant DB: {ex.Message}");
        }
    }

    private async Task WriteValidationSnapshot(
        IAutomationOutput output,
        string facilityId,
        string reportId)
    {
        try
        {
            if (!Guid.TryParse(reportId, out var scheduleId))
            {
                output.WriteLine("[Snapshot][Validation]          Invalid report ID; skipping validation snapshot.");
                return;
            }

            var entries = await _reader.GetReportEntriesAsync(scheduleId);
            var byReportingStatus = entries
                .GroupBy(e => string.IsNullOrWhiteSpace(e.ReportingStatus) ? "Unknown" : e.ReportingStatus!)
                .Select(g => $"{g.Key}={g.Count()}")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            output.WriteLine($"[Snapshot][Validation]          ReportingStatus: {string.Join(", ", byReportingStatus)}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"[Snapshot][Validation] Error querying Validation API-derived snapshot: {ex.Message}");
        }
    }
}
