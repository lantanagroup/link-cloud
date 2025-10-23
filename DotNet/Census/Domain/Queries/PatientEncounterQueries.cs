// Updated PatientEncounterQueries.cs (interface and implementation)
using LantanaGroup.Link.Census.Application.Models.Api;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEncounterQueries
{
    Task<PatientEncounter> GetPatientEncounterByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
    Task<PagedConfigModel<PatientEncounterModel>> GetPagedViewAsOf(string facilityId, DateTime threshold, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<PatientEncounterModel>> GetPagedCurrentPatientEncounters(string facilityId, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
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

    public async Task<PatientEncounter> GetPatientEncounterByCorrelationIdAsync(string correlationId,
        CancellationToken cancellationToken)
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

    public async Task<PagedConfigModel<PatientEncounterModel>> GetPagedCurrentPatientEncounters(
        string facilityId,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));

        _logger.LogInformation("Retrieving current patient encounters for Facility ID: {facilityId}", facilityId.Replace("\r", "").Replace("\n", ""));

        var query = _context.PatientEncounters
            .AsNoTracking()
            .Include(x => x.PatientIdentifiers)
            .Include(x => x.PatientVisitIdentifiers)
            .Where(x => x.FacilityId == facilityId);

        if (!string.IsNullOrEmpty(correlationId))
            query = query.Where(x => x.CorrelationId == correlationId);

        query = ApplySorting(query, sortBy, sortOrder);

        // Apply pagination
        var total = await query.CountAsync(cancellationToken);
        var pagedRecords = await query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .Select(e => new PatientEncounterModel
        {
            Id = e.Id,
            CorrelationId = e.CorrelationId,
            FacilityId = e.FacilityId,
            MedicalRecordNumber = e.MedicalRecordNumber,
            AdmitDate = e.AdmitDate,
            DischargeDate = e.DischargeDate,
            EncounterType = e.EncounterType,
            EncounterStatus = e.EncounterStatus,
            EncounterClass = e.EncounterClass,
            CreateDate = e.CreateDate,
            ModifyDate = e.ModifyDate,

            // INLINE MAPPING — EF Core can translate this
            PatientVisitIdentifiers = e.PatientVisitIdentifiers.Select(pvi => new PatientVisitIdentifierModel
            {
                Id = pvi.Id,
                PatientEncounterId = pvi.PatientEncounterId,
                Identifier = pvi.Identifier,
                SourceType = pvi.SourceType,
                CreateDate = pvi.CreateDate
            }).ToList(),

            PatientIdentifiers = e.PatientIdentifiers.Select(pi => new PatientIdentifierModel
            {
                Id = pi.Id,
                PatientEncounterId = pi.PatientEncounterId,
                Identifier = pi.Identifier,
                SourceType = pi.SourceType,
                CreateDate = pi.CreateDate
            }).ToList()
        })
        .ToListAsync(cancellationToken);

        return new PagedConfigModel<PatientEncounterModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = total == 0 ? 0 : (total + pageSize - 1) / pageSize
            },
            Records = pagedRecords
        };
    }

    public async Task<PagedConfigModel<PatientEncounterModel>> GetPagedViewAsOf(
        string facilityId,
        DateTime threshold,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("Facility ID is required.", nameof(facilityId));
        if (pageSize <= 0) pageSize = 10;
        if (pageNumber <= 0) pageNumber = 1;

        var query = _context.PatientEncounters
            .AsNoTracking()
            .Include(x => x.PatientIdentifiers)
            .Include(x => x.PatientVisitIdentifiers)
            .Where(e => e.FacilityId == facilityId && e.AdmitDate <= threshold);

        if (!string.IsNullOrEmpty(correlationId))
            query = query.Where(e => e.CorrelationId == correlationId);

        query = ApplySorting(query, sortBy, sortOrder);

        // Group by CorrelationId and select the latest event for each encounter
        var latestEvents = await query
            .GroupBy(x => x.CorrelationId)
            .Select(g => g.OrderByDescending(e => e.ModifyDate).FirstOrDefault())
            .ToListAsync(cancellationToken);

        // Filter non-null events
        var filteredEvents = latestEvents.Where(e => e != null).ToList();

        // Apply in-memory sorting
        if (!string.IsNullOrEmpty(sortBy))
        {
            var property = typeof(PatientEvent).GetProperty(sortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                if (sortOrder == SortOrder.Descending)
                {
                    filteredEvents = filteredEvents.OrderByDescending(e => property.GetValue(e)).ToList();
                }
                else
                {
                    filteredEvents = filteredEvents.OrderBy(e => property.GetValue(e)).ToList();
                }
            }
        }

        // Pagination in memory
        var total = filteredEvents.Count;
        var pagedEvents = filteredEvents
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Map to PatientEncounterModel
        var encounterModels = pagedEvents
            .Select(e => new PatientEncounterModel
            {
                CorrelationId = e.CorrelationId,
                FacilityId = e.FacilityId,
                MedicalRecordNumber = e.MedicalRecordNumber,
                AdmitDate = e.CreateDate,
                DischargeDate = e.DischargeDate,
                EncounterType = e.EncounterType.ToString(),
                EncounterStatus = e.EncounterStatus,
                EncounterClass = e.EncounterClass,
                CreateDate = e.CreateDate,
                ModifyDate = e.ModifyDate,
                PatientVisitIdentifiers = e.PatientVisitIdentifiers.Select(PatientVisitIdentifierModel.FromDomain).ToList(),
                PatientIdentifiers = e.PatientIdentifiers.Select(PatientIdentifierModel.FromDomain).ToList()
            })
            .ToList();

        return new PagedConfigModel<PatientEncounterModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = (total + pageSize - 1) / pageSize // Ceiling division
            },
            Records = encounterModels
        };
    }

    public async Task RebuildPatientEncounterTable(CancellationToken cancellationToken = default)
    {
        // Create a transaction for the entire operation
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Clear tables - use provider-agnostic approach
            _context.PatientIdentifiers.RemoveRange(_context.PatientIdentifiers);
            _context.PatientVisitIdentifiers.RemoveRange(_context.PatientVisitIdentifiers);
            _context.PatientEncounters.RemoveRange(_context.PatientEncounters);
            await _context.SaveChangesAsync(cancellationToken);

            // 2. Use standard LINQ query to get events
            _logger.LogInformation("Starting event retrieval");
            var startTime = DateTime.UtcNow;

            var allEvents = await _context.PatientEvents
                .Where(e => e.CorrelationId != null && e.CorrelationId != "")
                .OrderBy(e => e.CorrelationId)
                .ThenBy(e => e.ModifyDate)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {count} events in {time}ms",
                allEvents.Count, (DateTime.UtcNow - startTime).TotalMilliseconds);

            // 3. Group events by CorrelationId
            var eventsByCorrelation = allEvents.GroupBy(e => e.CorrelationId).ToList();

            // 4. Process correlation groups in parallel
            var newEncounters = new ConcurrentDictionary<string, PatientEncounter>();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            var processedCount = 0;
            var totalGroups = eventsByCorrelation.Count;
            var lockObj = new object();

            await Parallel.ForEachAsync(eventsByCorrelation, options, async (correlationGroup, ct) =>
            {
                PatientEncounter encounter = null;
                string correlationId = correlationGroup.Key;

                // Process each event for this correlation ID in chronological order
                foreach (var evt in correlationGroup.OrderBy(e => e.ModifyDate))
                {
                    var payload = evt.Payload;

                    // For admit events - create a new encounter
                    if (evt.EventType == EventType.FHIRListAdmit && payload is FHIRListAdmitPayload admitPayload)
                    {
                        encounter = admitPayload.CreatePatientEncounter(evt.FacilityId, evt.CorrelationId);

                        // Set basic properties
                        if (encounter != null)
                        {
                            // Ensure ID is set - generate a new one if null
                            if (string.IsNullOrEmpty(encounter.Id))
                            {
                                encounter.Id = Guid.NewGuid().ToString();
                            }

                            encounter.MedicalRecordNumber = evt.MedicalRecordNumber;
                            encounter.AdmitDate = evt.CreateDate;
                            encounter.ModifyDate = evt.ModifyDate;
                            encounter.EncounterType = evt.EventType.ToString();

                            // Ensure all PatientIdentifiers have IDs
                            foreach (var identifier in encounter.PatientIdentifiers)
                            {
                                if (string.IsNullOrEmpty(identifier.Id))
                                {
                                    identifier.Id = Guid.NewGuid().ToString();
                                }
                            }

                            // Ensure all PatientVisitIdentifiers have IDs
                            foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                            {
                                if (string.IsNullOrEmpty(visitIdentifier.Id))
                                {
                                    visitIdentifier.Id = Guid.NewGuid().ToString();
                                }
                            }
                        }
                    }
                    // For discharge or update events - update existing encounter
                    else if (encounter != null)
                    {
                        try
                        {
                            // Only try to update if we already have an encounter
                            encounter = payload.UpdatePatientEncounter(encounter);
                            encounter.ModifyDate = evt.ModifyDate;
                            encounter.EncounterType = evt.EventType.ToString();

                            // Re-check identifiers after update
                            foreach (var identifier in encounter.PatientIdentifiers)
                            {
                                if (string.IsNullOrEmpty(identifier.Id))
                                {
                                    identifier.Id = Guid.NewGuid().ToString();
                                }
                            }

                            foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                            {
                                if (string.IsNullOrEmpty(visitIdentifier.Id))
                                {
                                    visitIdentifier.Id = Guid.NewGuid().ToString();
                                }
                            }
                        }
                        catch (NotImplementedException)
                        {
                            // Skip update if not implemented
                        }
                    }
                }

                // Add the final state of the encounter to our concurrent dictionary
                if (encounter != null)
                {
                    // Final ID check before adding
                    if (string.IsNullOrEmpty(encounter.Id))
                    {
                        encounter.Id = Guid.NewGuid().ToString();
                    }

                    // One last check for all related entities
                    foreach (var identifier in encounter.PatientIdentifiers)
                    {
                        if (string.IsNullOrEmpty(identifier.Id))
                        {
                            identifier.Id = Guid.NewGuid().ToString();
                        }

                        // Ensure the relationship is properly set
                        identifier.PatientEncounterId = encounter.Id;
                    }

                    foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                    {
                        if (string.IsNullOrEmpty(visitIdentifier.Id))
                        {
                            visitIdentifier.Id = Guid.NewGuid().ToString();
                        }

                        // Ensure the relationship is properly set
                        visitIdentifier.PatientEncounterId = encounter.Id;
                    }

                    newEncounters.TryAdd(correlationId, encounter);
                }

                // Log progress - using thread-safe counter
                int current;
                lock (lockObj)
                {
                    processedCount++;
                    current = processedCount;

                    // Log progress periodically
                    if (current % 500 == 0 || current == totalGroups)
                    {
                        _logger.LogInformation("Processed {processed}/{total} correlation groups",
                            current, totalGroups);
                    }
                }
            });

            // 5. Add new encounters to the table in batches
            if (newEncounters.Count > 0)
            {
                const int batchSize = 500;
                var encountersList = newEncounters.Values.ToList();

                _logger.LogInformation("Adding {count} encounters in batches of {batchSize}",
                    encountersList.Count, batchSize);

                for (int i = 0; i < encountersList.Count; i += batchSize)
                {
                    var batch = encountersList.Skip(i).Take(batchSize).ToList();

                    // Final check for all encounters and related entities before saving
                    foreach (var encounter in batch)
                    {
                        if (string.IsNullOrEmpty(encounter.Id))
                        {
                            encounter.Id = Guid.NewGuid().ToString();
                        }

                        // Check PatientIdentifiers
                        foreach (var identifier in encounter.PatientIdentifiers)
                        {
                            if (string.IsNullOrEmpty(identifier.Id))
                            {
                                identifier.Id = Guid.NewGuid().ToString();
                            }

                            // Ensure relationship is set
                            identifier.PatientEncounterId = encounter.Id;
                        }

                        // Check PatientVisitIdentifiers
                        foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                        {
                            if (string.IsNullOrEmpty(visitIdentifier.Id))
                            {
                                visitIdentifier.Id = Guid.NewGuid().ToString();
                            }

                            // Ensure relationship is set
                            visitIdentifier.PatientEncounterId = encounter.Id;
                        }
                    }

                    await _context.PatientEncounters.AddRangeAsync(batch, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Added batch {current}/{total}",
                        Math.Min(i + batchSize, encountersList.Count), encountersList.Count);
                }
            }

            // Commit all changes in a single transaction
            await transaction.CommitAsync(cancellationToken);

            var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation(
                "Successfully rebuilt PatientEncounter table with {count} encounters in {time} seconds",
                newEncounters.Count, totalTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding PatientEncounter table: {message}", ex.Message);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    #region Private Methods
    private IQueryable<PatientEncounter> ApplySorting(
    IQueryable<PatientEncounter> query,
    string? sortBy,
    SortOrder? sortOrder)
    {
        var order = sortOrder ?? SortOrder.Ascending;
        var field = (sortBy ?? "").Trim().ToLower();

        return field switch
        {
            "admitdate" => order == SortOrder.Ascending
                ? query.OrderBy(e => e.AdmitDate)
                : query.OrderByDescending(e => e.AdmitDate),

            "dischargedate" => order == SortOrder.Ascending
                ? query.OrderBy(e => e.DischargeDate)
                : query.OrderByDescending(e => e.DischargeDate),

            "medicalrecordnumber" or "mrn" => order == SortOrder.Ascending
                ? query.OrderBy(e => e.MedicalRecordNumber)
                : query.OrderByDescending(e => e.MedicalRecordNumber),

            "correlationid" => order == SortOrder.Ascending
                ? query.OrderBy(e => e.CorrelationId)
                : query.OrderByDescending(e => e.CorrelationId),

            _ => query.OrderBy(e => e.CreateDate) // default fallback
        };
    }
    #endregion
}