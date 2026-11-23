using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Tenant.Business;

namespace UnitTests.Tenant;

/// <summary>
/// Pins what converting a facility API model into the Tenant entity produces, so the conversion can
/// change how it is implemented without changing its result.
/// </summary>
public class FacilityModelMappingTests
{
    [Fact]
    public void ToEntity_AllFieldsSet_CopiesEachValue()
    {
        var model = CreateFullModel();

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.Equal(model.Id, entity.Id);
        Assert.Equal("facility-1", entity.FacilityId);
        Assert.Equal("Facility One", entity.FacilityName);
        Assert.Equal("America/Chicago", entity.TimeZone);
        Assert.True(entity.IsDeleted);
        Assert.Equal(model.VendorVersionId, entity.VendorVersionId);
        Assert.Equal(new[] { "NHSNdQMAcuteCareHospitalInitialPopulation" }, entity.ScheduledReports.Daily);
        Assert.Equal(new[] { "NHSNGlycemicControlHypoglycemicInitialPopulation" }, entity.ScheduledReports.Weekly);
        Assert.Equal(new[] { "NHSNRespiratoryPathogensSurveillanceInitialPopulation", "Second" },
                     entity.ScheduledReports.Monthly);
    }

    [Fact]
    public void ToEntity_NullId_ReturnsEmptyGuid()
    {
        var model = CreateFullModel();
        model.Id = null;

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.Equal(Guid.Empty, entity.Id);
    }

    [Fact]
    public void ToEntity_NullIsDeleted_ReturnsFalse()
    {
        var model = CreateFullModel();
        model.IsDeleted = null;

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public void ToEntity_NullFacilityId_ReturnsNull()
    {
        var model = CreateFullModel();
        model.FacilityId = null;

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.Null(entity.FacilityId);
    }

    [Fact]
    public void ToEntity_NullScheduleArrays_ReturnsEmptyArrays()
    {
        var model = CreateFullModel();
        model.ScheduledReports = new TenantScheduledReportConfig();

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.NotNull(entity.ScheduledReports);
        Assert.Empty(entity.ScheduledReports.Daily);
        Assert.Empty(entity.ScheduledReports.Weekly);
        Assert.Empty(entity.ScheduledReports.Monthly);
    }

    [Fact]
    public void ToEntity_NullScheduledReports_ReturnsNull()
    {
        var model = CreateFullModel();
        model.ScheduledReports = null!;

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.Null(entity.ScheduledReports);
    }

    [Fact]
    public void ToEntity_ScheduleArrays_ReturnsCopiesNotSameInstances()
    {
        var model = CreateFullModel();

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.NotSame(model.ScheduledReports.Daily, entity.ScheduledReports.Daily);
        Assert.NotSame(model.ScheduledReports.Weekly, entity.ScheduledReports.Weekly);
        Assert.NotSame(model.ScheduledReports.Monthly, entity.ScheduledReports.Monthly);
    }

    [Fact]
    public void ToEntity_VendorSet_LeavesVendorVersionNull()
    {
        var model = CreateFullModel();
        model.Vendor = new VendorModel
        {
            Id = Guid.NewGuid(),
            Name = "Epic"
        };

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.Null(entity.VendorVersion);
    }

    [Fact]
    public void ToEntity_Always_LeavesCreateAndModifyDatesAtDefault()
    {
        var model = CreateFullModel();

        var entity = FacilityModelMapping.ToEntity(model);

        Assert.Equal(default, entity.CreateDate);
        Assert.Null(entity.ModifyDate);
    }

    private static FacilityModel CreateFullModel()
    {
        return new FacilityModel
        {
            Id = Guid.NewGuid(),
            FacilityId = "facility-1",
            FacilityName = "Facility One",
            TimeZone = "America/Chicago",
            IsDeleted = true,
            VendorVersionId = Guid.NewGuid(),
            ScheduledReports = new TenantScheduledReportConfig
            {
                Daily = ["NHSNdQMAcuteCareHospitalInitialPopulation"],
                Weekly = ["NHSNGlycemicControlHypoglycemicInitialPopulation"],
                Monthly = ["NHSNRespiratoryPathogensSurveillanceInitialPopulation", "Second"]
            }
        };
    }
}
