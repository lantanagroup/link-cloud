using LantanaGroup.Link.DMRP.Business.Mapping;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using Xunit;

namespace UnitTests.DMRP
{
    [Trait("Category", "UnitTests")]
    public class FacilityReportingPlanMapperTests
    {
        private static FacilityReportingPlan Entity(DateTimeKind createKind, DateTime? modifyDate = null) => new()
        {
            Id = "11111111-1111-1111-1111-111111111111",
            FacilityId = "F1",
            MeasureMappingId = "22222222-2222-2222-2222-222222222222",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = true,
            CreateDate = DateTime.SpecifyKind(new DateTime(2026, 5, 1, 12, 0, 0), createKind),
            ModifyDate = modifyDate
        };

        [Fact]
        public void ToModel_TreatsStoredTimestampsAsUtc()
        {
            // SQL Server's datetime2 columns carry no offset, so a CreateDate that survives a
            // round trip through EF Core comes back DateTimeKind.Unspecified, while a value set
            // in memory just before SaveChangesAsync (see UpdateBaseEntityInterceptor) is still
            // DateTimeKind.Utc. System.Text.Json only appends the "Z" suffix for Kind.Utc, so
            // Create (no round trip) rendered a "Z" while Get/Update (fetched from the DB first)
            // did not - the same field serialized two different ways depending on which
            // operation produced it.
            var entity = Entity(DateTimeKind.Unspecified);

            var model = FacilityReportingPlanMapper.ToModel(entity);

            Assert.Equal(DateTimeKind.Utc, model.CreateDate.Kind);
            Assert.Equal(12, model.CreateDate.Hour);
        }

        [Fact]
        public void ToModel_WithAnAlreadyUtcCreateDate_LeavesTheValueUnchanged()
        {
            var entity = Entity(DateTimeKind.Utc);

            var model = FacilityReportingPlanMapper.ToModel(entity);

            Assert.Equal(DateTimeKind.Utc, model.CreateDate.Kind);
            Assert.Equal(12, model.CreateDate.Hour);
        }

        [Fact]
        public void ToModel_WithNoModifyDate_LeavesItNull()
        {
            var entity = Entity(DateTimeKind.Utc);

            Assert.Null(FacilityReportingPlanMapper.ToModel(entity).ModifyDate);
        }

        [Fact]
        public void ToModel_TreatsAStoredModifyDateAsUtc()
        {
            var entity = Entity(DateTimeKind.Unspecified,
                modifyDate: DateTime.SpecifyKind(new DateTime(2026, 5, 2, 12, 0, 0), DateTimeKind.Unspecified));

            var model = FacilityReportingPlanMapper.ToModel(entity);

            Assert.Equal(DateTimeKind.Utc, model.ModifyDate!.Value.Kind);
            Assert.Equal(12, model.ModifyDate.Value.Hour);
        }
    }
}
