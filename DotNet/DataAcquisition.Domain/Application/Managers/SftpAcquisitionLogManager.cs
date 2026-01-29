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
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}


public class SftpAcquisitionLogManager(ILogger<SftpAcquisitionLogManager> logger, IDatabase database) : ISftpAcquisitionLogManager
{
    public async Task<SftpAcquisitionLogModel> CreateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.AddTag(DiagnosticNames.FacilityId, model.FacilityId);
        
        if (string.IsNullOrEmpty(model.FacilityId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "FacilityId cannot be empty or null");
            logger.LogError("Sftp Acquisition Log FacilityId cannot be empty or null");
            throw new MissingFacilityIdException($"A facility must be specified in order to create a SFTP Acquisition Log: Missing {nameof(model.FacilityId)}");
        }

        try
        {
            var entity = model.ToDomain();

            if (model.ProcessDate.HasValue)
            {
                entity.ProcessDate = model.ProcessDate.Value.ToUniversalTime();
            }

            // Capture the current trace context
            entity.OriginatingTraceId = Activity.Current?.TraceId.ToString();
            entity.OriginatingSpanId = Activity.Current?.SpanId.ToString();

            await database.SftpAcquisitionLogRepository.AddAsync(entity, cancellationToken);
            await database.SaveChangesAsync();

            logger.LogInformation("Created SFTP Acquisition Log for FacilityId: {FacilityId}, FileNames: {FileNames}", model.FacilityId.Sanitize(), string.Join(", ", model.FileNames));
        
            return entity.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error creating SFTP Acquisition Log for FacilityId: {FacilityId}, FileNames: {FileNames}", model.FacilityId.Sanitize(), string.Join(", ", model.FileNames));
            throw;
        }
    }

    public async Task<SftpAcquisitionLogModel> UpdateAsync(SftpAcquisitionLogModel model, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);
        

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
            existingLog.FileNames = model.FileNames;
            existingLog.RetryAttempts = model.RetryAttempts;
            existingLog.Notes = model.Notes;

            if (model.ProcessDate.HasValue)
            {
                existingLog.ProcessDate = model.ProcessDate.Value.ToUniversalTime();
            }

            await database.SaveChangesAsync();

            return existingLog.ToModel();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("SftpAcquisitionLog", JsonSerializer.Serialize(model, LinkFhirSerializerOptions.ActivityTagging));
            activity?.AddTag(DiagnosticNames.StackTrace, ex.StackTrace);
            logger.LogError(ex, "Error updating SFTP Acquisition Log for FacilityId: {FacilityId}, FileNames: {FileNames}", model.FacilityId, string.Join(", ", model.FileNames));
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