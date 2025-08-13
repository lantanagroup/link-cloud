using LantanaGroup.Link.Census.Application.Models.Api;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEncounterQueries
{
    Task<PatientEncounter> GetPatientEncounterByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
    Task<IEnumerable<PatientEncounterModel>> GetViewAsOf(string facilityId, DateTime threshold, string? correlationIdh = null, CancellationToken cancellationToken = default);
    Task RebuildPatientEncounterTable(CancellationToken cancellationToken = default);

}

public class PatientEncounterQueries : IPatientEncounterQueries
{
    private readonly ILogger<PatientEncounterQueries> _logger;
    private readonly CensusContext _context;

    public PatientEncounterQueries(ILogger<PatientEncounterQueries> logger, CensusContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PatientEncounter> GetPatientEncounterByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(correlationId));
        }

        _logger.LogInformation("Retrieving patient encounters for Correlation ID: {correlationId}", correlationId);

        var encounter = await _context
            .PatientEncounters
            .Include(x => x.PatientIdentifiers)
            .Where(x => x.CorrelationId == correlationId)
            .FirstOrDefaultAsync(cancellationToken);

        // Ensure a value is returned in all code paths
        return encounter;
    }

    public async Task<IEnumerable<PatientEncounterModel>> GetViewAsOf(string facilityId, DateTime threshold, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        //materialize a view based on the patientEvent table as of the given threshold date
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        if (threshold == default)
            throw new ArgumentException("Threshold date cannot be default.", nameof(threshold));


        _logger.LogInformation("Retrieving patient encounters for Facility ID: {facilityId} as of {threshold}", facilityId.Replace("\r", "").Replace("\n", ""), threshold);

        var query = _context.PatientEvents
        .Where(x => x.FacilityId == facilityId && x.ModifyDate <= threshold);

        if (!string.IsNullOrEmpty(correlationId))
            query = query.Where(x => x.CorrelationId == correlationId);

        // Group by CorrelationId and select the latest event for each encounter
        var latestEvents = await query
            .GroupBy(x => x.CorrelationId)
            .Select(g => g.OrderByDescending(e => e.ModifyDate).FirstOrDefault())
            .ToListAsync(cancellationToken);

        // Map PatientEvent to PatientEncounterModel (implement your own mapping logic)
        var encounterModels = latestEvents
            .Where(e => e != null)
            .Select(e => new PatientEncounterModel
            {
                CorrelationId = e.CorrelationId,
                FacilityId = e.FacilityId,
                MedicalRecordNumber = e.MedicalRecordNumber,
                AdmitDate = e.CreateDate,
                DischargeDate = null, // Set if available in PatientEvent
                EncounterType = e.EventType.ToString(),
                EncounterStatus = null, // Set if available in PatientEvent
                EncounterClass = null, // Set if available in PatientEvent
                CreateDate = e.CreateDate,
                ModifyDate = e.ModifyDate,
                PatientVisitIdentifiers = new List<PatientVisitIdentifierModel>(), // Populate if needed
                PatientIdentifiers = new List<PatientIdentifierModel>() // Populate if needed
            });

        return encounterModels;
    }

    public async Task RebuildPatientEncounterTable(CancellationToken cancellationToken = default)
    {
        // 1. Remove all existing PatientEncounters
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM [PatientIdentifiers]; DELETE FROM [PatientVisitIdentifiers];DELETE FROM [PatientEncounters];", cancellationToken);

        // 2. Get latest PatientEvent for each CorrelationId (no facilityId, threshold, or correlationId filter)
        var latestEventsQuery =
            from evt in _context.PatientEvents
            where evt.CorrelationId != null && evt.CorrelationId != ""
            join maxEvt in (
                from e in _context.PatientEvents
                where e.CorrelationId != null && e.CorrelationId != ""
                group e by e.CorrelationId into g
                select new { CorrelationId = g.Key, MaxModifyDate = g.Max(x => x.ModifyDate) }
            ) on new { evt.CorrelationId, evt.ModifyDate } equals new { maxEvt.CorrelationId, ModifyDate = maxEvt.MaxModifyDate }
            select evt;

        var latestEvents = await latestEventsQuery.ToListAsync(cancellationToken);

        // 3. Build PatientEncounter entities from events
        var newEncounters = new List<PatientEncounter>();
        foreach (var evt in latestEvents)
        {
            if (evt == null) continue;

            var payload = evt.GetPayload();
            PatientEncounter encounter = payload?.CreatePatientEncounter(evt.FacilityId, evt.CorrelationId);

            if (encounter != null)
            {
                encounter = payload.UpdatePatientEncounter(encounter);

                encounter.MedicalRecordNumber = evt.MedicalRecordNumber;
                encounter.AdmitDate = evt.CreateDate;
                encounter.ModifyDate = evt.ModifyDate;
                encounter.EncounterType = evt.EventType.ToString();

                newEncounters.Add(encounter);
            }
        }

        // 4. Add new encounters to the table
        if (newEncounters.Count > 0)
        {
            await _context.PatientEncounters.AddRangeAsync(newEncounters, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
