using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEncounterQueries
{
    Task<PatientEncounterModel?> GetByIdAsync(Guid id, string facilityId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<PatientEncounterModel>> SearchAsync(SearchPatientEncounterModel searchModel, CancellationToken cancellationToken = default);
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

    public async Task<PatientEncounterModel?> GetByIdAsync(Guid id, string facilityId, CancellationToken cancellationToken = default)
    {
        return await _context.PatientEncounters
            .AsNoTracking()
            .Include(x => x.PatientIdentifiers)
            .Include(x => x.PatientVisitIdentifiers)
            .Where(x => x.Id == id && x.FacilityId == facilityId)
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
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedConfigModel<PatientEncounterModel>> SearchAsync(SearchPatientEncounterModel searchModel, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchModel.FacilityId))
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(searchModel.FacilityId));

        if (searchModel.PageSize <= 0) searchModel.PageSize = 10;
        if (searchModel.PageNumber <= 0) searchModel.PageNumber = 1;

        _logger.LogInformation("Searching patient encounters for Facility ID: {facilityId}", searchModel.FacilityId.Replace("\r", "").Replace("\n", ""));

        var query = _context.PatientEncounters
            .AsNoTracking()
            .Include(x => x.PatientIdentifiers)
            .Include(x => x.PatientVisitIdentifiers)
            .Where(x => x.FacilityId == searchModel.FacilityId);

        if (!string.IsNullOrEmpty(searchModel.CorrelationId))
            query = query.Where(x => x.CorrelationId == searchModel.CorrelationId);

        if (searchModel.Threshold.HasValue)
        {
            var threshold = searchModel.Threshold.Value;
            query = query.Where(e => e.AdmitDate <= threshold && (e.DischargeDate == null || e.DischargeDate > threshold));
        }

        query = ApplySorting(query, searchModel.SortBy, searchModel.SortOrder);

        var total = await query.CountAsync(cancellationToken);

        var pagedRecords = await query
            .Skip((searchModel.PageNumber - 1) * searchModel.PageSize)
            .Take(searchModel.PageSize)
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
                PageNumber = searchModel.PageNumber,
                PageSize = searchModel.PageSize,
                TotalCount = total,
                TotalPages = total == 0 ? 0 : (total + searchModel.PageSize - 1) / searchModel.PageSize
            },
            Records = pagedRecords
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
            var newEncounters = new ConcurrentDictionary<string, PatientEncounterModel>();
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
                PatientEncounterModel? encounter = null;
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
                            if (encounter.Id == default)
                            {
                                encounter.Id = Guid.NewGuid();
                            }

                            encounter.MedicalRecordNumber = evt.MedicalRecordNumber;
                            encounter.AdmitDate = evt.CreateDate;
                            encounter.ModifyDate = evt.ModifyDate;
                            encounter.EncounterType = evt.EventType.ToString();

                            // Ensure all PatientIdentifiers have IDs
                            foreach (var identifier in encounter.PatientIdentifiers)
                            {
                                if (identifier.Id == default)
                                {
                                    identifier.Id = Guid.NewGuid();
                                }
                            }

                            // Ensure all PatientVisitIdentifiers have IDs
                            foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                            {
                                if (visitIdentifier.Id == default)
                                {
                                    visitIdentifier.Id = Guid.NewGuid();
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
                                if (identifier.Id == default)
                                {
                                    identifier.Id = Guid.NewGuid();
                                }
                            }

                            foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                            {
                                if (visitIdentifier.Id == default)
                                {
                                    visitIdentifier.Id = Guid.NewGuid();
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
                    if (encounter.Id == default)
                    {
                        encounter.Id = Guid.NewGuid();
                    }

                    // One last check for all related entities
                    foreach (var identifier in encounter.PatientIdentifiers)
                    {
                        if (identifier.Id == default)
                        {
                            identifier.Id = Guid.NewGuid();
                        }

                        // Ensure the relationship is properly set
                        identifier.PatientEncounterId = encounter.Id;
                    }

                    foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                    {
                        if (visitIdentifier.Id == default)
                        {
                            visitIdentifier.Id = Guid.NewGuid();
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
                        if (encounter.Id == default)
                        {
                            encounter.Id = Guid.NewGuid();
                        }

                        // Check PatientIdentifiers
                        foreach (var identifier in encounter.PatientIdentifiers)
                        {
                            if (identifier.Id == default)
                            {
                                identifier.Id = Guid.NewGuid();
                            }

                            // Ensure relationship is set
                            identifier.PatientEncounterId = encounter.Id;
                        }

                        // Check PatientVisitIdentifiers
                        foreach (var visitIdentifier in encounter.PatientVisitIdentifiers)
                        {
                            if (visitIdentifier.Id == default)
                            {
                                visitIdentifier.Id = Guid.NewGuid();
                            }

                            // Ensure relationship is set
                            visitIdentifier.PatientEncounterId = encounter.Id;
                        }
                    }

                    await _context.PatientEncounters.AddRangeAsync(batch.Select(b => new PatientEncounter
                    {
                        FacilityId = b.FacilityId,
                        CorrelationId = b.CorrelationId,
                        AdmitDate = b.AdmitDate,
                        DischargeDate = b.DischargeDate,
                        EncounterClass = b.EncounterClass,
                        EncounterStatus = b.EncounterStatus,
                        EncounterType = b.EncounterType,
                        MedicalRecordNumber = b.MedicalRecordNumber,
                        PatientIdentifiers = b.PatientIdentifiers?.Select(i => new PatientIdentifier
                        {
                            Id = i.Id,                            
                            Identifier = i.Identifier,
                            SourceType = i.SourceType,
                            CreateDate = i.CreateDate,
                        }).ToList() ?? new(),
                        PatientVisitIdentifiers = b.PatientVisitIdentifiers?.Select(v => new PatientVisitIdentifier
                        {
                            Id = v.Id,
                            Identifier = v.Identifier,
                            SourceType = v.SourceType,
                            CreateDate = v.CreateDate,
                        }).ToList() ?? new(),
                    }), cancellationToken);
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
    SortOrder sortOrder)
    {
        var field = (sortBy ?? "").Trim().ToLower();

        return field switch
        {
            "admitdate" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(e => e.AdmitDate)
                : query.OrderByDescending(e => e.AdmitDate),
            "dischargedate" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(e => e.DischargeDate)
                : query.OrderByDescending(e => e.DischargeDate),
            "medicalrecordnumber" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(e => e.MedicalRecordNumber)
                : query.OrderByDescending(e => e.MedicalRecordNumber),
            "correlationid" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(e => e.CorrelationId)
                : query.OrderByDescending(e => e.CorrelationId),
            "modifydate" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(e => e.ModifyDate)
                : query.OrderByDescending(e => e.ModifyDate),
            _ => query.OrderBy(e => e.CreateDate)
        };
    }
    #endregion
}