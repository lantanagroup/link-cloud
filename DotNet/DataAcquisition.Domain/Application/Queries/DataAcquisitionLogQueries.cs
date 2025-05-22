using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using DataAcquisition.Domain.Infrastructure;
using DataAcquisition.Domain.Infrastructure.Context;
using System.Linq;
using DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using System.Data.Entity;
using DataAcquisition.Domain.Application.Models;

namespace DataAcquisition.Domain.Application.Queries;

public interface IDataAcquisitionLogQueries
{
    Task<List<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default);  
}

public class DataAcquisitionLogQueries : IDataAcquisitionLogQueries
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;

    public DataAcquisitionLogQueries(IDatabase database, DataAcquisitionDbContext dbContext)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Here is the T-SQL equivalent of the LINQ query:
    /// select distinct l.patientId, l.ReportTrackingId, l.CorrelationId 
    /// from DataAcquisitionLog l
    /// where l.FacilityId = :1
    /// and l.Status not in ('Pending', 'Processing')
    /// and not exists(select 1 from DataAcquisitionLog l1 where l.ReportTrackingId = l1.ReportTrackingId and l.CorrelationId = l1.ReportTrackingId and l.Status in ('Pending', 'Processing'))
    /// and NOT ISNULL(l.patientId, '') = ''
    /// group by l.patientId, l.facilityId, l.ReportTrackingId, l.CorrelationId
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default)
    {
        var query = from log in _dbContext.DataAcquisitionLogs
                    where !new RequestStatus?[] { RequestStatus.Pending, RequestStatus.Processing }.Contains(log.Status)
                    && !(from l1 in _dbContext.DataAcquisitionLogs
                         where l1.ScheduledReport.ReportTrackingId == log.ScheduledReport.ReportTrackingId
                               && l1.CorrelationId == log.CorrelationId
                               && new RequestStatus?[] { RequestStatus.Pending, RequestStatus.Processing }.Contains(l1.Status)
                               && l1.TailSent
                         select l1).Any()
                         && !((log.PatientId ?? "").Trim() == "")
                         && !log.TailSent
                    group log by new { 
                        log.PatientId, 
                        log.FacilityId, 
                        log.CorrelationId, 
                        log.QueryPhase,
                        log.ScheduledReport,
                        log.ReportableEvent,

                    } into g
                    select new TailingMessageModel {
                        Key = g.Key.FacilityId,
                        CorrelationId = g.Key.CorrelationId,
                        ResourceAcquired = new ResourceAcquired
                        {
                            PatientId = g.Key.PatientId,
                            QueryType = g.Key.QueryPhase.ToString(),
                            ReportableEvent = g.Key.ReportableEvent.Value,
                            AcquisitionComplete = true,
                            ScheduledReports = new List<ScheduledReport> { g.Key.ScheduledReport }
                        }
                    };

        return await query.ToListAsync();
    }
}
