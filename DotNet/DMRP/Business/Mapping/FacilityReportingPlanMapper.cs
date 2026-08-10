using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DMRP.Business.Mapping
{
    public static class FacilityReportingPlanMapper
    {
        public static FacilityReportingPlanModel ToModel(FacilityReportingPlan entity) => new()
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            MeasureMappingId = entity.MeasureMappingId,
            ReportingMonth = entity.ReportingMonth,
            ReportingYear = entity.ReportingYear,
            IsReporting = entity.IsReporting,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };

        public static FacilityReportingPlan ToEntity(FacilityReportingPlanRequest request) => new()
        {
            FacilityId = request.FacilityId?.Sanitize() ?? string.Empty,
            MeasureMappingId = request.MeasureMappingId?.Sanitize() ?? string.Empty,
            ReportingMonth = request.ReportingMonth,
            ReportingYear = request.ReportingYear,
            IsReporting = request.IsReporting
        };
    }
}
