using System.Diagnostics;
using System.Text.Json;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface ISftpAcquisitionLogManager
{
    Task<SftpAcquisitionLogModel> CreateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken);
    Task<SftpAcquisitionLogModel> UpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken);
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}


public class SftpAcquisitionLogManager(ILogger<SftpAcquisitionLogManager> logger, IDatabase database) : ISftpAcquisitionLogManager
{
    public async Task<SftpAcquisitionLogModel> CreateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.AddTag(DiagnosticNames.FacilityId, model.FacilityId);

        try
        {
            var entity = model.ToDomain();
        
            await database.SftpAcquisitionLogRepository.AddAsync(entity, cancellationToken);
            await database.SaveChangesAsync();
            
            logger.LogInformation("Created SFTP Acquisition Log for FacilityId: {FacilityId}, FacilityName: {FacilityName}", model.FacilityId, model.FacilityName);
        
            return entity.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error creating SFTP Acquisition Log for FacilityId: {FacilityId}, FacilityName: {FacilityName}", model.FacilityId, model.FacilityName);
            throw;
        }
    }

    public async Task<SftpAcquisitionLogModel> UpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);
        
        if(model.Id <= 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Log ID cannot be zero or null");
            logger.LogError("Sftp Acquisition Log Id cannot be zero or null");
            throw new ArgumentException("SftpAcquisitionLog Id must be greater than zero for update operation.");
        }

        if (string.IsNullOrEmpty(model.FacilityId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FacilityId cannot be empty or null");
            logger.LogError("Sftp Acquisition Log FacilityId cannot be empty or null");
            throw new MissingFacilityIdException(nameof(model.FacilityId));
        }

        var existingLog = await database.SftpAcquisitionLogRepository.GetAsync(model.Id, cancellationToken);
        
        //TODO: should update the interface to allow a nullable return from GetAsync
        if (existingLog is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SFTP Acquisition log not found");
            throw new NotFoundException($"SFTP Acquisition log with ID {model.Id} not found.");
        }

        try
        {
            // Update existing log entry
            existingLog.EncounterCount = model.EncounterCount;
            existingLog.PatientCount = model.PatientCount;
            existingLog.ProcessDate = model.ProcessDate;
        
            await database.SaveChangesAsync();
        
            return existingLog.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("SftpAcquisitionLog", JsonSerializer.Serialize(model, LinkFhirSerializerOptions.ActivityTagging));
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error updating SFTP Acquisition Log for FacilityId: {FacilityId}, FacilityName: {FacilityName}", model.FacilityId, model.FacilityName);
            throw;
        }

        
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);
        
        // Check if log exists
        var existingLog = await database.SftpAcquisitionLogRepository.GetAsync(id, cancellationToken);
        
        if (existingLog is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SFTP Acquisition log not found");
            throw new NotFoundException($"SFTP Acquisition log with ID {id} not found.");
        }

        try
        {
            database.SftpAcquisitionLogRepository.Remove(existingLog);
            await database.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error deleting SFTP Acquisition Log with ID: {Id}", id);
            throw;
        }
    }
}