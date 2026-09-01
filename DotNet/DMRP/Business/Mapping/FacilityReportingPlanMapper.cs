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
            Component = entity.Component,
            ReportingMonth = entity.ReportingMonth,
            ReportingYear = entity.ReportingYear,
            IsReporting = entity.IsReporting,

            // The measure is stored on the row, so it answers even for an enrollment Link has
            // no mapping for. The dQM and frequency are the mapping's, and stay null until one
            // exists - and until then reads that never load the navigation leave them null
            // rather than costing a join.
            Measure = entity.Measure,
            DQM = entity.MeasureMapping?.DQM,
            Frequency = entity.MeasureMapping?.Frequency,

            // The columns are datetime2 with no offset, so a value that round-tripped through
            // EF Core comes back DateTimeKind.Unspecified, while a value set in memory just
            // before SaveChangesAsync (UpdateBaseEntityInterceptor) is still DateTimeKind.Utc.
            // System.Text.Json only appends "Z" for Kind.Utc, so left unqualified, Create
            // (no round trip) rendered a "Z" suffix while Get/Update (fetched from the DB
            // first) did not - the same field serialized two different ways depending on which
            // operation produced it. The values are always UTC in practice, so it's safe to
            // pin the Kind rather than convert it.
            CreateDate = DateTime.SpecifyKind(entity.CreateDate, DateTimeKind.Utc),
            ModifyDate = entity.ModifyDate is null
                ? null
                : DateTime.SpecifyKind(entity.ModifyDate.Value, DateTimeKind.Utc)
        };

        public static FacilityReportingPlan ToEntity(FacilityReportingPlanRequest request) => new()
        {
            FacilityId = request.FacilityId?.Sanitize() ?? string.Empty,

            // Blank becomes null rather than an empty string: an absent mapping is the state of
            // an enrollment nobody has mapped yet, and an empty foreign key is not a value the
            // column can hold.
            MeasureMappingId = NullIfBlank(request.MeasureMappingId?.Sanitize()),
            Measure = request.Measure?.Sanitize() ?? string.Empty,

            // Defaulted rather than required: every enrollment recorded before components existed
            // came from the medicine operation, and a caller that does not know about components
            // still means MSC.
            Component = ReportingComponents.Normalize(
                string.IsNullOrWhiteSpace(request.Component) ? ReportingComponents.Msc : request.Component.Sanitize()),
            ReportingMonth = request.ReportingMonth,
            ReportingYear = request.ReportingYear,
            IsReporting = request.IsReporting
        };

        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
