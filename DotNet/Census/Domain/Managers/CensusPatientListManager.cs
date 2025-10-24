using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Domain.Repositories;
using LantanaGroup.Link.Census.Models;

namespace LantanaGroup.Link.Census.Domain.Managers;

public interface ICensusPatientListManager
{
    Task<CensusPatientListModel> CreateAsync(CreateCensusPatientListModel model, CancellationToken cancellationToken = default);
    Task<CensusPatientListModel> UpdateAsync(UpdateCensusPatientListModel model, CancellationToken cancellationToken = default);
}

public class CensusPatientListManager : ICensusPatientListManager
{
    private readonly ILogger<CensusPatientListManager> _logger;
    private readonly IDatabase _database;

    public CensusPatientListManager(ILogger<CensusPatientListManager> logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
    }

    public async Task<CensusPatientListModel> CreateAsync(CreateCensusPatientListModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        var entity = new CensusPatientListEntity
        {
            FacilityId = model.FacilityId,
            PatientId = model.PatientId,
            DisplayName = model.DisplayName,
            AdmitDate = model.AdmitDate,
            IsDischarged = model.IsDischarged,
            DischargeDate = model.DischargeDate,
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow
        };

        entity = await _database.CensusPatientListRepository.AddAsync(entity, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);   

        return CensusPatientListModel.FromDomain(entity);
    }

    public async Task<CensusPatientListModel> UpdateAsync(UpdateCensusPatientListModel model, CancellationToken cancellationToken = default)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        var existing = await _database.CensusPatientListRepository.FirstOrDefaultAsync(x => x.FacilityId == model.FacilityId && x.PatientId == model.PatientId, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"CensusPatientList for FacilityId {model.FacilityId} and PatientId not found.");
        }

        existing.DisplayName = model.DisplayName;
        existing.AdmitDate = model.AdmitDate;
        existing.IsDischarged = model.IsDischarged;
        existing.DischargeDate = model.DischargeDate;
        existing.ModifyDate = DateTime.UtcNow;

        _database.CensusPatientListRepository.Update(existing);

        await _database.SaveChangesAsync(cancellationToken);

        return CensusPatientListModel.FromDomain(existing);
    }
}

