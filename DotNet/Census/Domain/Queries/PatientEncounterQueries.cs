using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Concurrent;
using System.Data;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEncounterQueries
{
    Task<PatientEncounter> GetPatientEncounterByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
    Task<PagedConfigModel<PatientEncounterModel>> GetPagedViewAsOf(string facilityId, DateTime threshold, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<PatientEncounterModel>> GetPagedCurrentPatientEncounters(string facilityId, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task RebuildPatientEncounterTable(string? facilityId = default, string? correlationId = default, bool useTransaction = true, CancellationToken cancellationToken = default);
    Task<IEnumerable<PatientEncounterModel>> GetAdmittedPatientEncounterModelsByDateRange(string facilityId, DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetCurrentlyAdmittedPatientsForFacility(string facilityId, CancellationToken cancellationToken = default);
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

        if (threshold == default)
            return await GetPagedCurrentPatientEncounters(facilityId, correlationId, sortBy, sortOrder, pageSize, pageNumber, cancellationToken);

        var query = _context.PatientEncounters
            .AsNoTracking()
            .Include(x => x.PatientIdentifiers)
            .Include(x => x.PatientVisitIdentifiers)
            .Where(e => e.FacilityId == facilityId &&
                        e.AdmitDate <= threshold &&
                        (e.DischargeDate == null || e.DischargeDate > threshold));

        if (!string.IsNullOrEmpty(correlationId))
            query = query.Where(e => e.CorrelationId == correlationId);

        // Apply sorting in SQL
        query = ApplySorting(query, sortBy, sortOrder);

        // Count in SQL
        var total = await query.CountAsync(cancellationToken);

        // Paginate and project in SQL
        var pagedEvents = await query
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
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
            },
            Records = pagedEvents
        };
    }

    private async Task Rebuild(string? facilityId = default, string? correlationId = default, CancellationToken cancellationToken = default)
    {
        var piRange = _context.PatientIdentifiers.Select(pi => pi);
        var pvRange = _context.PatientVisitIdentifiers.Select(pvi => pvi);
        var peRange = _context.PatientEncounters.Include(e => e.PatientIdentifiers).Include(e => e.PatientVisitIdentifiers).Select(e => e);

        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            var baseQuery = peRange.Where(pi => pi.FacilityId == facilityId);
            piRange = baseQuery.SelectMany(e => e.PatientIdentifiers);
            pvRange = baseQuery.SelectMany(e => e.PatientVisitIdentifiers);
            peRange = baseQuery;
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var baseQuery = peRange.Where(pi => pi.CorrelationId == correlationId);
            piRange = baseQuery.SelectMany(e => e.PatientIdentifiers);
            pvRange = baseQuery.SelectMany(e => e.PatientVisitIdentifiers);
            peRange = baseQuery;
        }

        // 1. Clear tables - use provider-agnostic approach
        _context.PatientIdentifiers.RemoveRange(piRange);
        _context.PatientVisitIdentifiers.RemoveRange(pvRange);
        _context.PatientEncounters.RemoveRange(peRange);
        await _context.SaveChangesAsync(cancellationToken);

        // 2. Use standard LINQ query to get events
        _logger.LogInformation("Starting event retrieval");
        var startTime = DateTime.UtcNow;

        var allEventsQuery = _context.PatientEvents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            allEventsQuery = allEventsQuery.Where(e => e.FacilityId == facilityId);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            allEventsQuery = allEventsQuery.Where(e => e.CorrelationId == correlationId);
        }

        var allEvents = await allEventsQuery
            .Where(e => e.CorrelationId != null && e.CorrelationId != "")
            .OrderBy(e => e.CorrelationId)
            .ThenBy(e => e.EventDate)
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

                    // Ensure ID is set - generate a new one if null
                    if (string.IsNullOrEmpty(encounter.Id))
                    {
                        encounter.Id = Guid.NewGuid().ToString();
                    }

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
                // For discharge or update events - update existing encounter
                else if (encounter != null && IsUpdateablePayload(payload))
                {
                    encounter = payload.UpdatePatientEncounter(encounter);
                    encounter.ModifyDate = evt.ModifyDate;
                    
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
    }

    public async Task RebuildPatientEncounterTable(string? facilityId = default, string? correlationId = default, bool useTransaction = true, CancellationToken cancellationToken = default)
    {

        if (useTransaction)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await Rebuild(facilityId, correlationId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error rebuilding PatientEncounter table: {message}", ex.Message);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            await Rebuild(facilityId, correlationId, cancellationToken);
        }
    }

    public async Task<IEnumerable<string>> GetCurrentlyAdmittedPatientsForFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        
        var currentDateTime = DateTime.UtcNow;
        return _context.PatientEncounters
            .AsNoTracking()
            .Include(e => e.PatientIdentifiers)
            .Include(e => e.PatientVisitIdentifiers)
            .Where(e => e.FacilityId == facilityId
                && (e.AdmitDate <= currentDateTime && (e.DischargeDate == null || e.DischargeDate >= currentDateTime)))
            .Select(x => x.PatientIdentifiers.FirstOrDefault().Identifier);
    }

    public async Task<IEnumerable<PatientEncounterModel>> GetAdmittedPatientEncounterModelsByDateRange(string facilityId, DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        
        return _context.PatientEncounters
            .AsNoTracking()
            .Include(e => e.PatientIdentifiers)
            .Include(e => e.PatientVisitIdentifiers)
            .Where(e => e.FacilityId == facilityId
                && (e.AdmitDate <= endDateTime && (e.DischargeDate == null || e.DischargeDate >= startDateTime)))
            .Select(x => PatientEncounterModel.FromDomain(x));
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
            "medicalrecordnumber" => order == SortOrder.Ascending
                ? query.OrderBy(e => e.MedicalRecordNumber)
                : query.OrderByDescending(e => e.MedicalRecordNumber),
            "correlationid" => order == SortOrder.Ascending
                ? query.OrderBy(e => e.CorrelationId)
                : query.OrderByDescending(e => e.CorrelationId),
            "modifydate" => order == SortOrder.Ascending
                ? query.OrderBy(e => e.ModifyDate)
                : query.OrderByDescending(e => e.ModifyDate),
            _ => query.OrderBy(e => e.CreateDate)
        };
    }

    private bool IsUpdateablePayload(IPayload payload)
    {
        return payload switch
        {
            FHIRListDischargePayload => true,
            _ => false
        };
    }
    #endregion
}