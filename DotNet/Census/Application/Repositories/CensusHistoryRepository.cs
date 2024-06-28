using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class CensusHistoryRepository : EntityRepository<PatientCensusHistoricEntity>, ICensusHistoryRepository
{
    private readonly ILogger<CensusHistoryRepository> _logger;
    private readonly CensusContext _context;

    public CensusHistoryRepository(ILogger<CensusHistoryRepository> logger, CensusContext context) : base(logger, context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }


    public async Task<IEnumerable<PatientCensusHistoricEntity>> GetAllCensusReportsForFacility(string facilityId)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var facilityIdStr = facilityId;

        //var reports = (await _context.PatientCensusHistorics.ToListAsync()).Where(x => x.FacilityId == facilityIdStr);
        var reports = await _context.PatientCensusHistorics.Where(x => x.FacilityId == facilityIdStr).ToListAsync();
        return reports;
    }

    private bool CheckIfNullOrString(object value, string varName)
    {
        if (value == null) throw new ArgumentNullException(varName);
        if (value.GetType() != typeof(string)) throw new ArgumentException($"{varName} is required to be a string!");
        return true;
    }
}
