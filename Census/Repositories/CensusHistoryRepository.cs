using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Census.Repositories;

public class CensusHistoryRepository : MongoDbRepository<PatientCensusHistoricEntity>, ICensusHistoryRepository
{
    public CensusHistoryRepository(IOptions<MongoConnection> mongoSettings) : base(mongoSettings)
    {
    }

    public override async Task<PatientCensusHistoricEntity> GetAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PatientCensusHistoricEntity>.Filter.Eq(x => x.ReportId, reportId);
        var historicReport = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();
        return historicReport;
    }

    public override PatientCensusHistoricEntity Get(string reportId)
    {
        var filter = Builders<PatientCensusHistoricEntity>.Filter.Eq(x => x.ReportId, reportId);
        var historicReport = _collection.Find(filter).FirstOrDefault();
        return historicReport;
    }

    public List<PatientCensusHistoricEntity> GetAllCensusReportsForFacility(string facilityId)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var facilityIdStr = (string)facilityId;

        var reports = _collection.Find(x => x.FacilityId == facilityIdStr).ToList();
        return reports;
    }

    public override PatientCensusHistoricEntity Update(PatientCensusHistoricEntity entity)
    {
        throw new NotImplementedException("Updates not allowed for this collection.");
    }

    public override async Task<PatientCensusHistoricEntity> UpdateAsync(PatientCensusHistoricEntity Entity, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Updates not allowed for this collection.");
    }

    public override void Delete(string id)
    {
        throw new NotImplementedException("Deletes not allowed for this collection.");
    }

    public override async System.Threading.Tasks.Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Deletes not allowed for this collection.");
    }

    private bool CheckIfNullOrString(object value, string varName)
    {
        if (value == null) throw new ArgumentNullException(varName);
        if (value.GetType() != typeof(string)) throw new ArgumentException($"{varName} is required to be a string!");
        return true;
    }
}
