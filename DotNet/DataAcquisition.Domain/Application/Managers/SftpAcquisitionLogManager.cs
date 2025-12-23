using System.Diagnostics;
using System.Text.Json;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface ISftpAcquisitionLogManager
{
    Task<SftpAcquisitionLogModel> CreateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken);
    Task<SftpAcquisitionLogModel> UpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken);

    Task<SftpAcquisitionLogModel> UserUpdateAsync(SftpAcquisitionLogModel model,
        CancellationToken cancellationToken);
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}


public class SftpAcquisitionLogManager(ILogger<SftpAcquisitionLogManager> logger, IDatabase database) : ISftpAcquisitionLogManager
{
    public async Task<SftpAcquisitionLogModel> CreateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.AddTag(DiagnosticNames.FacilityId, model.FacilityId);

        // Ensure the process date is set sometime in the future
        if (model.ProcessDate <= DateTime.Now)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "ProcessDate cannot be in the past.");
            activity?.AddTag("sftp.acquisition.log", JsonSerializer.Serialize(model, LinkFhirSerializerOptions.ActivityTagging));
            throw new ArgumentException("ProcessDate cannot be in the past.");
        }

        try
        {
            var entity = model.ToDomain();
            
            // There should not be any counts yet as it has not been run
            entity.EncounterCount = 0;
            entity.PatientCount = 0;
        
            await database.SftpAcquisitionLogRepository.AddAsync(entity, cancellationToken);
            await database.SaveChangesAsync();
            
            logger.LogInformation("Created SFTP Acquisition Log for FacilityId: {FacilityId}, FileName: {FileName}", model.FacilityId.Sanitize(), model.FileName.Sanitize());
        
            return entity.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error creating SFTP Acquisition Log for FacilityId: {FacilityId}, FileName: {FileName}", model.FacilityId.Sanitize(), model.FileName.Sanitize());
            throw;
        }
    }

    public async Task<SftpAcquisitionLogModel> UpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);
        
        if(model.ExternalId is not null && model.ExternalId != Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid log id");
            logger.LogError("Sftp Acquisition Log Id cannot be null or empty for update operation.");
            throw new ArgumentException("SftpAcquisitionLog Id cannot be null or empty for update operation.");
        }

        if (string.IsNullOrEmpty(model.FacilityId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FacilityId cannot be empty or null");
            logger.LogError("Sftp Acquisition Log FacilityId cannot be empty or null");
            throw new MissingFacilityIdException(nameof(model.FacilityId));
        }

        var existingLog = await database.SftpAcquisitionLogRepository.FirstOrDefaultAsync(x => x.ExternalId == model.ExternalId, cancellationToken);
        
        if (existingLog is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SFTP Acquisition log not found");
            throw new DomainEntityNotFoundException($"SFTP Acquisition log with ID {model.ExternalId} not found.");
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
            logger.LogError(ex, "Error updating SFTP Acquisition Log for FacilityId: {FacilityId}, FileName: {FileName}", model.FacilityId, model.FileName);
            throw;
        }
    }
    
    public async Task<SftpAcquisitionLogModel> UserUpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, model.ExternalId);

        if (model.ProcessDate <= DateTime.Now)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "ProcessDate cannot be in the past.");
            activity?.AddTag("sftp.log.process.date", model.ProcessDate);
            throw new ArgumentException("ProcessDate cannot be in the past.");
        }

        if (model.ExternalId is null || model.ExternalId == Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "ExternalId cannot be null");
            throw new ArgumentNullException(nameof(model.ExternalId));
        }

        // TODO: Consider adding file type restrictions
        if (string.IsNullOrEmpty(model.FileName))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FileName cannot be null or empty");
            throw new ArgumentNullException(nameof(model.FileName));
        }

        var existingLog = await database.SftpAcquisitionLogRepository.FirstOrDefaultAsync(x => x.ExternalId == model.ExternalId, cancellationToken);
        
        if (existingLog is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SFTP Acquisition log not found");
            throw new DomainEntityNotFoundException($"SFTP Acquisition log with ID {model.ExternalId} not found.");
        }
        
        try
        {
            // Update existing log entry
            existingLog.FileName = model.FileName;
            existingLog.ProcessDate = model.ProcessDate;
        
            await database.SaveChangesAsync();
        
            return existingLog.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("SftpAcquisitionLog", JsonSerializer.Serialize(model, LinkFhirSerializerOptions.ActivityTagging));
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error updating SFTP Acquisition Log for FacilityId: {FacilityId}, FileName: {FileName}", model.FacilityId.Sanitize(), model.FileName.Sanitize());
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