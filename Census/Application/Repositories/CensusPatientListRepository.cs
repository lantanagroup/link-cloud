using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Census.Application.Repositories;

public class CensusPatientListRepository : BaseSqlConfigurationRepo<CensusPatientListEntity>, ICensusPatientListRepository
{
    private readonly ILogger<CensusPatientListRepository> _logger;
    private readonly CensusContext _context;

    public CensusPatientListRepository(ILogger<CensusPatientListRepository> logger, CensusContext context) : base(logger, context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<CensusPatientListEntity>> GetActivePatientsForFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        
        var activePatients = _context.CensusPatientLists.Where(x => x.FacilityId == facilityId && !x.IsDischarged).ToList();
        return activePatients;
    }

    public async Task<List<CensusPatientListEntity>> GetAllPatientsForFacility(string facilityId, CancellationToken cancellationToken = default)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));

        var allPatients = _context.CensusPatientLists.Where(x => x.FacilityId == facilityId).ToList();
        return allPatients;
    }

    private bool CheckIfNullOrString(object value, string varName)
    {
        if (value == null) throw new ArgumentNullException(varName);
        if (value.GetType() != typeof(string)) throw new ArgumentException($"{varName} is required to be a string!");
        return true;
    }
}
