using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using PatientEvent = LantanaGroup.Link.Census.Domain.Entities.POI.PatientEvent;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using LantanaGroup.Link.Census.Application.Models.Api;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using Microsoft.EntityFrameworkCore.Query; // Important for SQL functions

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEventQueries
{
    Task<PatientEvent> GetLatestEventByFacilityAndPatientId(string facilityId, string patientId,
        CancellationToken cancellationToken);

    Task<IEnumerable<PatientEvent>> GetPatientEvents(string facilityId, string? correlationId = default,
        DateTime? startDate = default, DateTime? endDate = default, CancellationToken cancellationToken = default);

    Task DeletePatientEventByCorrelationId(string correlationId, CancellationToken cancellationToken);
    Task<IEnumerable<PatientEventModel>> GetAdmittedPatientEventModelsByDateRange(string facilityId, DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default);
}

public class PatientEventQueries : IPatientEventQueries
{
    private readonly CensusContext _context;

    public PatientEventQueries(CensusContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
    
    public async Task<IEnumerable<PatientEventModel>> GetAdmittedPatientEventModelsByDateRange(string facilityId,  DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));

        if(startDateTime == default)
            throw new ArgumentException("Start date cannot be default.", nameof(startDateTime));

        if(endDateTime == default)
            throw new ArgumentException("End date cannot be default.", nameof(endDateTime));

        var admitEventTypes = new List<EventType>
        {
            EventType.FHIRListAdmit,
            EventType.A01
        };

        var dischargeEventTypes = new List<EventType>
        {
            EventType.FHIRListDischarge,
            EventType.A03
        };

        // Get all admit and discharge events within the date range for the facility
        var eventsWithinRange = await GetPatientEvents(facilityId, null, startDateTime, endDateTime, cancellationToken);
        
        // Group events by patient ID
        var patientEvents = eventsWithinRange
            .GroupBy(x => x.SourcePatientId)
            .ToDictionary(g => g.Key, g => g.ToList());


        // Find the patients who have an admit event within the date range
        // and either have no discharge event or the latest event is an admit event
        var result = new List<PatientEventModel>();
    
        foreach (var patientGroup in patientEvents)
        {
            var sourcePatientId = patientGroup.Key;
            var events = patientGroup.Value;
        
            // Check if there's at least one admit event in the range
            var hasAdmitEvent = events.Any(e => admitEventTypes.Contains(e.EventType));
        
            if (hasAdmitEvent)
            {
                // Find the latest event for this patient
                var latestEvent = events
                    .OrderByDescending(e => e.CreateDate)
                    .FirstOrDefault();
            
                // If the latest event is an admit event, include this patient
                if (latestEvent != null && admitEventTypes.Contains(latestEvent.EventType))
                {
                    result.Add(PatientEventModel.FromDomain(latestEvent));
                }
            }
        }

        return result;
    }

    public async Task DeletePatientEventByCorrelationId(string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
        }
        
        // Check if we're using the InMemory provider
        bool isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        if (isInMemory)
        {
            // For InMemory provider, load entities and remove them
            var entities = await _context.PatientEvents
                .Where(x => x.CorrelationId == correlationId)
                .ToListAsync(cancellationToken);
        
            _context.PatientEvents.RemoveRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // For SQL providers, use batch delete
            await _context.PatientEvents
                .Where(x => x.CorrelationId == correlationId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    public async Task<PatientEvent> GetLatestEventByFacilityAndPatientId(string facilityId, string patientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID cannot be null or empty.", nameof(patientId));
        }

        return _context.PatientEvents.Where(x => x.FacilityId == facilityId && x.SourcePatientId == patientId)
            .OrderByDescending(x => x.CreateDate).FirstOrDefault();
    }

    public async Task<IEnumerable<PatientEvent>> GetPatientEvents(
        string facilityId,
        string? correlationId = default,
        DateTime? startDate = default,
        DateTime? endDate = default,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }

        var query = _context.PatientEvents.AsQueryable();
        query = query.Where(x => x.FacilityId == facilityId);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(x => x.CorrelationId == correlationId);
        }

        // For regular events or as a fallback, filter by CreateDate
        var baseQuery = query;

        if (startDate.HasValue && startDate != default)
        {
            baseQuery = baseQuery.Where(x => x.CreateDate >= startDate.Value);
        }

        if (endDate.HasValue && endDate != default)
        {
            baseQuery = baseQuery.Where(x => x.CreateDate <= endDate.Value);
        }

        // Create specific queries for FHIR events with date filtering
        var queries = new List<IQueryable<PatientEvent>>();

        // Add the base query for non-FHIR events
        queries.Add(baseQuery.Where(x =>
            x.EventType != EventType.FHIRListAdmit &&
            x.EventType != EventType.FHIRListDischarge));

        // Add query for FHIRListAdmit events
        var admitQuery = query.Where(x => x.EventType == EventType.FHIRListAdmit);
        
        if (startDate.HasValue && startDate != default)
        {
            admitQuery = admitQuery.Where(x =>
                ((FHIRListAdmitPayload)x.Payload).AdmitDate >= startDate.Value);
        }

        if (endDate.HasValue && endDate != default)
        {
            admitQuery = admitQuery.Where(x =>
                ((FHIRListAdmitPayload)x.Payload).AdmitDate <= endDate.Value);
        }

        queries.Add(admitQuery);

        // Add query for FHIRListDischarge events
        var dischargeQuery = query.Where(x => x.EventType == EventType.FHIRListDischarge);

        if (startDate.HasValue && startDate != default)
        {
            dischargeQuery = dischargeQuery.Where(x =>
                ((FHIRListDischargePayload)x.Payload).DischargeDate >= startDate.Value);
        }

        if (endDate.HasValue && endDate != default)
        {
            dischargeQuery = dischargeQuery.Where(x =>
                ((FHIRListDischargePayload)x.Payload).DischargeDate <= endDate.Value);
        }

        queries.Add(dischargeQuery);

        // Combine all the queries using Union
        var combinedQuery = queries[0];
        for (int i = 1; i < queries.Count; i++)
        {
            combinedQuery = combinedQuery.Union(queries[i]);
        }

        return await combinedQuery.ToListAsync(cancellationToken);
    }
}