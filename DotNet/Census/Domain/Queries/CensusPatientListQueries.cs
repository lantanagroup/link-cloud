using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities;
using LantanaGroup.Link.Census.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface ICensusPatientListQueries
{
    Task<CensusPatientListModel?> GetAsync(string facilityId, string patientId, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<CensusPatientListModel>> SearchAsync(SearchCensusPatientListModel model, CancellationToken cancellationToken = default);
}

public class CensusPatientListQueries : ICensusPatientListQueries
{
    private readonly CensusContext _dbContext;
    private readonly ILogger<CensusPatientListQueries> _logger;

    public CensusPatientListQueries(CensusContext dbContext, ILogger<CensusPatientListQueries> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CensusPatientListModel?> GetAsync(string facilityId, string patientId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.CensusPatientLists
            .FirstOrDefaultAsync(x => x.FacilityId == facilityId && x.PatientId == patientId, cancellationToken);

        return entity != null ? CensusPatientListModel.FromDomain(entity) : null;
    }

    public async Task<PagedConfigModel<CensusPatientListModel>> SearchAsync(SearchCensusPatientListModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var query = _dbContext.CensusPatientLists.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(model.FacilityId))
        {
            query = query.Where(c => c.FacilityId == model.FacilityId);
        }

        if (!string.IsNullOrEmpty(model.PatientId))
        {
            query = query.Where(c => c.PatientId == model.PatientId);
        }

        if (model.ActiveOnly)
        {
            query = query.Where(c => !c.IsDischarged);
        }

        if (model.AdmitDateStart.HasValue && !model.AdmitDateEnd.HasValue)
        {
            var endDate = DateTime.UtcNow;
            query = query.Where(c => c.AdmitDate >= model.AdmitDateStart.Value && c.AdmitDate <= endDate);
        }
        else if (!model.AdmitDateStart.HasValue && model.AdmitDateEnd.HasValue)
        {
            query = query.Where(c => (c.DischargeDate == null || c.DischargeDate <= model.AdmitDateEnd.Value) && c.AdmitDate <= model.AdmitDateEnd.Value);
        }
        else if (model.AdmitDateStart.HasValue && model.AdmitDateEnd.HasValue)
        {
            query = query.Where(c => c.AdmitDate <= model.AdmitDateEnd.Value && (c.DischargeDate == null || c.DischargeDate >= model.AdmitDateStart.Value));
        }

        if (model.DischargeDateStart.HasValue)
        {
            query = query.Where(c => c.DischargeDate >= model.DischargeDateStart.Value);
        }

        if (model.DischargeDateEnd.HasValue)
        {
            query = query.Where(c => c.DischargeDate <= model.DischargeDateEnd.Value);
        }

        bool applyDistinct = model.DistinctByPatientId || (!model.AdmitDateStart.HasValue && !model.AdmitDateEnd.HasValue) || (model.AdmitDateStart.HasValue && !model.AdmitDateEnd.HasValue);
        if (applyDistinct)
        {
            query = query.GroupBy(c => c.PatientId).Select(g => g.OrderByDescending(c => c.ModifyDate).FirstOrDefault());
        }

        var total = await query.CountAsync(cancellationToken);

        query = model.SortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<CensusPatientListEntity>(model.SortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<CensusPatientListEntity>(model.SortBy)),
            _ => query
        };

        var patients = await query
            .Skip((model.PageNumber - 1) * model.PageSize)
            .Take(model.PageSize)
            .Select(c => new CensusPatientListModel
            {
                FacilityId = c.FacilityId,
                PatientId = c.PatientId,
                DisplayName = c.DisplayName,
                AdmitDate = c.AdmitDate,
                IsDischarged = c.IsDischarged,
                DischargeDate = c.DischargeDate,
                CreateDate = c.CreateDate,
                ModifyDate = c.ModifyDate
            })
            .ToListAsync(cancellationToken);

        return new PagedConfigModel<CensusPatientListModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = model.PageNumber,
                PageSize = model.PageSize,
                TotalCount = total,
                TotalPages = (long)Math.Ceiling(total / (double)model.PageSize)
            },
            Records = patients
        };
    }

    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var type = typeof(T);
        var inputSortBy = sortBy?.Trim();
        string sortKey = "Id"; // default

        if (!string.IsNullOrEmpty(inputSortBy))
        {
            var prop = type.GetProperty(inputSortBy, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                sortKey = prop.Name;
            }
        }

        var parameter = Expression.Parameter(type, "p");
        var property = Expression.Property(parameter, sortKey);
        var converted = Expression.Convert(property, typeof(object));
        return Expression.Lambda<Func<T, object>>(converted, parameter);
    }
}
