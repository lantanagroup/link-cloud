using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEncounterQueries
{
    public Task<PatientEncounter> GetPatientEncounterByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
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
}
