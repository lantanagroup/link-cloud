using LantanaGroup.Link.DMRP.Config;
using LantanaGroup.Link.DMRP.Controllers;
using LantanaGroup.Link.DMRP.MockDmrp;
using LantanaGroup.Link.DMRP.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace UnitTests.DMRP;

/// <summary>
/// The Admin UI reads both flags from here instead of keeping its own copy of <c>DMRP:Enabled</c>.
/// </summary>
[Trait("Category", "UnitTests")]
public class DmrpStatusControllerTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public void GetStatus_ReportsBothFlags(bool dmrpEnabled, bool mockEnabled, bool expectedDmrp, bool expectedMock)
    {
        var result = CreateController(dmrpEnabled, mockEnabled).GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var model = Assert.IsType<DmrpStatusModel>(ok.Value);
        Assert.Equal(expectedDmrp, model.DmrpEnabled);
        Assert.Equal(expectedMock, model.MockDmrpEnabled);
    }

    /// <summary>
    /// The write-through runs inside the DMRP module, so it can never be on while DMRP is off.
    /// </summary>
    [Fact]
    public void GetStatus_DmrpDisabledMockSwitchedOn_ReportsTheMockOff()
    {
        var result = CreateController(dmrpEnabled: false, mockEnabled: true).GetStatus();

        var model = Assert.IsType<DmrpStatusModel>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(model.MockDmrpEnabled);
    }

    [Fact]
    public void Controller_UsesTheRouteTheAdminUiCalls()
    {
        var route = typeof(DmrpStatusController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();

        Assert.Equal("api/dmrp/dmrp-status", route.Template);
    }

    private static DmrpStatusController CreateController(bool dmrpEnabled, bool mockEnabled) =>
        new(Options.Create(new DmrpSettings { Enabled = dmrpEnabled }), new MockDmrpStatus(mockEnabled));
}
