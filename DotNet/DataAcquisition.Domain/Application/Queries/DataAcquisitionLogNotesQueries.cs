using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IDataAcquisitionLogNotesQueries
{
    Task<List<string>> GetByLogIdAsync(long logId, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogNotesQueries : IDataAcquisitionLogNotesQueries
{
    private readonly DataAcquisitionDbContext _dbContext;

    public DataAcquisitionLogNotesQueries(DataAcquisitionDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<string>> GetByLogIdAsync(long logId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DataAcquisitionLogNotes
            .AsNoTracking()
            .Where(n => n.DataAcquisitionLogId == logId)
            .OrderBy(n => n.Id)
            .Select(n => n.Note)
            .ToListAsync(cancellationToken);
    }
}
