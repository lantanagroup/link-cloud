using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Tenant;

/// <summary>
/// Every client error from the facility endpoints answers with RFC 7807 problem details.
/// </summary>
/// <remarks>
/// The refusals themselves are covered by <c>DmrpFacilityOperationsTests</c>; what is asserted here
/// is the shape the caller receives. <c>BadRequest(ex.Message)</c> passes a bare <c>string</c>,
/// which the client-error mapping does not convert to problem details - it goes out as
/// <c>text/plain</c> with no type, title or traceId - so a test that only checks the status code
/// passes either way.
/// </remarks>
[Trait("Category", "UnitTests")]
public class FacilityControllerProblemDetailsTests
{
    private const string FacilityId = "test-facility";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private const string ScheduleRefusal =
        "Scheduled reports cannot be set on a facility while DMRP is enabled. They are derived from the facility's DMRP reporting plans. Resubmit with empty daily, weekly and monthly arrays in scheduledReports.";

    private static readonly IServiceProvider MvcServices = BuildMvcServices();

    private static IServiceProvider BuildMvcServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }

    private static FacilityController CreateController(AutoMocker mocker)
    {
        // The constructor null-checks these and reads .Value, which an auto-mocked IOptions returns
        // null for. None of them is reached on the paths under test.
        mocker.Use<IOptions<ServiceRegistry>>(Options.Create(new ServiceRegistry()));
        mocker.Use<IOptions<LinkTokenServiceSettings>>(Options.Create(new LinkTokenServiceSettings()));
        mocker.Use<IOptions<LinkBearerServiceOptions>>(Options.Create(new LinkBearerServiceOptions()));

        var controller = mocker.CreateInstance<FacilityController>();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = MvcServices }
        };
        controller.ProblemDetailsFactory = MvcServices.GetRequiredService<ProblemDetailsFactory>();
        return controller;
    }

    private static FacilityModel Facility(string[]? monthly = null) => new()
    {
        FacilityId = FacilityId,
        FacilityName = "Test Facility",
        TimeZone = "America/Chicago",
        ScheduledReports = new TenantScheduledReportConfig
        {
            Daily = Array.Empty<string>(),
            Weekly = Array.Empty<string>(),
            Monthly = monthly ?? Array.Empty<string>()
        }
    };

    private static ProblemDetails AssertProblem(IActionResult? result, int expectedStatusCode, string expectedDetail)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatusCode, problem.Status);
        Assert.Equal(expectedDetail, problem.Detail);

        return problem;
    }

    [Fact]
    public async Task Create_with_a_caller_supplied_schedule_answers_with_problem_details()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFacilityOperations>()
            .Setup(o => o.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScheduledReportsNotAcceptedException(ScheduleRefusal));

        var result = await CreateController(mocker)
            .StoreFacility(Facility(new[] { "NHSNAcuteCareHospitalMonthlyInitialPopulation" }), CancellationToken.None);

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest, ScheduleRefusal);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal(BadRequestType, problem.Type);
    }

    [Fact]
    public async Task Update_with_a_caller_supplied_schedule_answers_with_problem_details()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFacilityQueries>()
            .Setup(q => q.GetAsync(FacilityId, null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(Facility());
        mocker.GetMock<IFacilityOperations>()
            .Setup(o => o.UpdateAsync(It.IsAny<FacilityModel>(), It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ScheduledReportsNotAcceptedException(ScheduleRefusal));

        var result = await CreateController(mocker).PutFacility(
            FacilityId,
            Facility(new[] { "NHSNAcuteCareHospitalMonthlyInitialPopulation" }),
            CancellationToken.None);

        var problem = AssertProblem(result.Result, StatusCodes.Status400BadRequest, ScheduleRefusal);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal(BadRequestType, problem.Type);
    }

    [Fact]
    public async Task Create_without_a_facility_id_names_the_field()
    {
        var facility = Facility();
        facility.FacilityId = null;

        var result = await CreateController(new AutoMocker()).StoreFacility(facility, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Facility ID is required.");
    }

    [Fact]
    public async Task Create_without_a_facility_name_names_the_field()
    {
        var facility = Facility();
        facility.FacilityName = null;

        var result = await CreateController(new AutoMocker()).StoreFacility(facility, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Facility name is required.");
    }

    /// <summary>
    /// The manager assembles its messages with <c>AppendLine</c>, so the message arrives with a
    /// trailing newline. <c>detail</c> is prose and must not carry it.
    /// </summary>
    [Fact]
    public async Task A_validation_message_reaches_detail_without_its_trailing_newline()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFacilityOperations>()
            .Setup(o => o.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationException("Timezone is required." + Environment.NewLine));

        var result = await CreateController(mocker).StoreFacility(Facility(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Timezone is required.");
    }

    /// <summary>
    /// Messages are also read from logs and asserted in other tests, so they are written as
    /// fragments. The terminating period belongs to the prose, and is added on the way out.
    /// </summary>
    [Fact]
    public async Task A_message_written_as_a_fragment_is_finished_as_a_sentence()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFacilityOperations>()
            .Setup(o => o.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationException("Timezone Not Found: Mars/Olympus_Mons"));

        var result = await CreateController(mocker).StoreFacility(Facility(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status400BadRequest, "Timezone Not Found: Mars/Olympus_Mons.");
    }

    [Fact]
    public async Task An_unknown_facility_answers_with_problem_details()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IFacilityQueries>()
            .Setup(q => q.GetAsync(FacilityId, null, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync((FacilityModel?)null);

        var result = await CreateController(mocker).GetFacility(FacilityId, CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound, $"Facility with Id: {FacilityId} Not Found.");
    }
}
