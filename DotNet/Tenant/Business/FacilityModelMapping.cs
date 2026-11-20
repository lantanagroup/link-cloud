using AutoMapper;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Data.Entities;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Business;

/// <summary>
/// Converts the facility API model into the Tenant entity that is saved and scheduled.
/// </summary>
public static class FacilityModelMapping
{
    // Built once rather than per call: the configuration is fixed, and compiling it is the
    // expensive half of using AutoMapper.
    private static readonly IMapper ModelToEntityMapper = new MapperConfiguration(cfg =>
    {
        cfg.CreateMap<FacilityModel, Facility>();
        cfg.CreateMap<TenantScheduledReportConfig, ScheduledReportModel>();
    }).CreateMapper();

    /// <summary>
    /// Returns a new <see cref="Facility"/> holding the values of <paramref name="model"/>.
    /// </summary>
    public static Facility ToEntity(FacilityModel model)
    {
        return ModelToEntityMapper.Map<FacilityModel, Facility>(model);
    }
}
