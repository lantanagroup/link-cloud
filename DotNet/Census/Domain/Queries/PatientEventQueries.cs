using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEventQueries 
{
    // Define methods for querying patient events, e.g.:
    // Task<List<PatientEvent>> GetEventsByPatientIdAsync(string patientId, CancellationToken cancellationToken);
    // Task<List<PatientEvent>> GetEventsByFacilityIdAsync(string facilityId, CancellationToken cancellationToken);
    Task<PatientEvent> GetLatestEventByFacilityAndPatientId(string facilityId, string patientId, CancellationToken cancellationToken);
}
public class PatientEventQueries : IPatientEventQueries
{
    private readonly CensusContext _context;
    public PatientEventQueries(CensusContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
}
