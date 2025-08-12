using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEventQueries 
{
    Task<PatientEvent> GetLatestEventByFacilityAndPatientId(string facilityId, string patientId, CancellationToken cancellationToken);
    Task<IEnumerable<PatientEvent>> GetPatientEvents(string facilityId, string? correlationId = default, DateTime? startDate = default, DateTime? endDate = default, CancellationToken cancellationToken = default);
    Task DeletePatientEventByCorrelationId(string correlationId, CancellationToken cancellationToken);
}
public class PatientEventQueries : IPatientEventQueries
{
    private readonly CensusContext _context;
    public PatientEventQueries(CensusContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task DeletePatientEventByCorrelationId(string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
        }
        return _context.PatientEvents.Where(x => x.CorrelationId == correlationId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<PatientEvent> GetLatestEventByFacilityAndPatientId(string facilityId, string patientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID cannot be null or empty.", nameof(patientId));
        }
        return _context.PatientEvents.Where(x => x.FacilityId == facilityId && x.SourcePatientId == patientId).OrderByDescending(x => x.CreateDate).FirstOrDefault();
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
        if (startDate.HasValue && startDate != default)
        {
            query = query.Where(x => x.CreateDate >= startDate.Value);
        }
        if (endDate.HasValue && endDate != default)
        {
            query = query.Where(x => x.CreateDate <= endDate.Value);
        }
        return await query.ToListAsync(cancellationToken);
    }
}
