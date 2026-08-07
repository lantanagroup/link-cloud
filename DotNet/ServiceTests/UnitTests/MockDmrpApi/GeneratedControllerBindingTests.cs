using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

using Task = System.Threading.Tasks.Task;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Pins down how the NSwag-generated abstract controller behaves once its methods are
/// overridden.
/// </summary>
/// <remarks>
/// Everything here is easier to verify than to reason about, so these tests drive real HTTP
/// through a real MVC pipeline and will fail loudly if an NSwag or ASP.NET upgrade changes
/// any of it.
/// <list type="number">
/// <item>Binding source and validation attributes on the generated base <em>do</em> reach the
/// override. MVC resolves them through the base declaration, so [FromQuery] and [BindRequired]
/// keep working even though <c>DmrpController</c> restates neither.</item>
/// <item>Routes come from the base too. <c>DmrpController</c> declares no [Route] of its own,
/// and the operations land at the paths the contract names.</item>
/// <item>Default parameter values do <em>not</em> carry over. An override that drops
/// <c>= default</c> gets no default at all.</item>
/// </list>
/// <para>
/// The contract is currently two operations with no optional parameters, so the third point
/// has only the cancellation token to demonstrate it. It is kept because the contract is
/// provisional: the published one is likely to add optional filters and paging, and that is
/// exactly when this trap bites. The related hazard -- NSwag typing optional string filters as
/// non-nullable, which [ApiController] then treats as required -- has no example on the
/// current base and will need re-pinning if optional filters return.
/// </para>
/// </remarks>
public class GeneratedControllerBindingTests
{
    private readonly ITestOutputHelper _output;

    public GeneratedControllerBindingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static async Task<IHost> StartHostAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                    services.AddControllers()
                            .AddApplicationPart(typeof(GeneratedControllerBindingTests).Assembly));
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        return host;
    }

    // ------------------------------------------------------- the C# level facts

    [Fact]
    public void ParameterAttributesReachTheOverrideOnlyViaTheStaticAttributeHelper()
    {
        // Two reflection APIs disagree, and the difference is why binding still works.
        // ParameterInfo.GetCustomAttributes ignores its inherit flag and reports only the
        // override's own attributes -- none. Attribute.GetCustomAttributes walks to the
        // base method's parameter, and that is the one MVC's behaviour matches.
        var overrideParameter = typeof(AnnotatedProbeController)
            .GetMethod(nameof(AnnotatedProbeController.GetMonthlyMedicineReportingPlan))!
            .GetParameters()[0];

        var viaParameterInfo = overrideParameter
            .GetCustomAttributes(inherit: true).Select(a => a.GetType().Name).ToArray();
        var viaAttributeHelper = Attribute
            .GetCustomAttributes(overrideParameter, inherit: true).Select(a => a.GetType().Name).ToArray();

        _output.WriteLine($"ParameterInfo.GetCustomAttributes(true): [{string.Join(", ", viaParameterInfo)}]");
        _output.WriteLine($"Attribute.GetCustomAttributes(p, true):  [{string.Join(", ", viaAttributeHelper)}]");

        viaParameterInfo.Should().BeEmpty();
        viaAttributeHelper.Should().Contain("FromQueryAttribute");
        viaAttributeHelper.Should().Contain("BindRequiredAttribute");
    }

    [Fact]
    public void DefaultParameterValuesAreNotInheritedByTheOverride()
    {
        // The base declares cancellationToken = default. The probe redeclares it without
        // one, and defaults are baked into the declaring signature rather than resolved
        // through the base -- so the generated default is silently lost.
        //
        // Harmless for a cancellation token, which MVC supplies anyway. Not harmless for
        // the pageSize = 10 that a fuller contract would bring, where the loss turns a
        // paged endpoint into an unpaged one with no compiler warning.
        var baseParameter = typeof(DmrpControllerBase)
            .GetMethod(nameof(DmrpControllerBase.GetMonthlyMedicineReportingPlan))!
            .GetParameters().Single(p => p.Name == "cancellationToken");
        var overrideParameter = typeof(AnnotatedProbeController)
            .GetMethod(nameof(AnnotatedProbeController.GetMonthlyMedicineReportingPlan))!
            .GetParameters().Single(p => p.Name == "cancellationToken");

        _output.WriteLine($"base default:     {baseParameter.HasDefaultValue}");
        _output.WriteLine($"override default: {overrideParameter.HasDefaultValue}");

        baseParameter.HasDefaultValue.Should().BeTrue();
        overrideParameter.HasDefaultValue.Should().BeFalse(
            "this probe deliberately omits it, which is exactly the mistake to avoid");
    }

    // ------------------------------------------------- what MVC actually does

    [Fact]
    public async Task RoutesComeFromTheGeneratedBase()
    {
        // The probe declares a class-level prefix but no method routes; the paths below
        // come entirely from the base. This is what lets DmrpController declare no routing
        // at all and still serve the contract's paths.
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        (await client.GetAsync("/annotated/msc?facilityId=F1&reportingMonth=5&reportingYear=2026"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync("/annotated/ps/annual?facilityId=F1&reportingYear=2026"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithApiController_QueryParametersStillBind()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync(
            "/annotated/msc?facilityId=F1&reportingMonth=5&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.FacilityId.Should().Be("F1");
        plan.ReportingMonth.Should().Be(5);
        plan.ReportingYear.Should().Be(2026);
    }

    [Fact]
    public async Task WithApiController_TheAnnualOperationBindsWithoutAMonth()
    {
        // Its signature has no month at all, so a request that omits one must succeed --
        // and the response carries none.
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/annotated/ps/annual?facilityId=F1&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.FacilityId.Should().Be("F1");
        plan.ReportingYear.Should().Be(2026);
        plan.ReportingMonth.Should().BeNull();
    }

    [Theory]
    [InlineData("/annotated/msc?reportingMonth=5&reportingYear=2026")]
    [InlineData("/annotated/msc?facilityId=F1&reportingYear=2026")]
    [InlineData("/annotated/msc?facilityId=F1&reportingMonth=5")]
    [InlineData("/annotated/ps/annual?reportingYear=2026")]
    [InlineData("/annotated/ps/annual?facilityId=F1")]
    public async Task WithApiController_MissingRequiredQueryParameterIsRejected(string url)
    {
        // [BindRequired] lives on the base's parameters and is never restated by the
        // override, so this is the proof that validation attributes inherit as well as
        // binding sources do.
        using var host = await StartHostAsync();

        (await host.GetTestClient().GetAsync(url)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "{0} omits a required parameter", url);
    }

    [Fact]
    public async Task WithApiController_ANonNumericMonthIsRejectedRatherThanCoerced()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync(
            "/annotated/msc?facilityId=F1&reportingMonth=May&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WithoutApiController_ExplicitBindingSourcesStillApply()
    {
        // [FromQuery] on the generated base still reaches MVC without [ApiController],
        // because MVC resolves parameter attributes through the base declaration. What
        // [ApiController] adds is inference for parameters with no source attribute, plus
        // automatic 400s for model-state failures.
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync(
            "/plain/msc?facilityId=F1&reportingMonth=5&reportingYear=2026");

        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();

        _output.WriteLine($"without [ApiController], bound FacilityId = '{plan?.FacilityId}'");
        plan!.FacilityId.Should().Be("F1", "the generated [FromQuery] is still honoured");
    }
}

/// <summary>Overrides the generated base and carries [ApiController].</summary>
[ApiController]
[Route("annotated")]
public class AnnotatedProbeController : ProbeControllerBase
{
}

/// <summary>Identical, but without [ApiController], to isolate what that attribute buys.</summary>
[Route("plain")]
public class PlainProbeController : ProbeControllerBase
{
}

/// <summary>
/// Echoes back whatever MVC managed to bind, so a test can tell binding success from silent
/// failure.
/// </summary>
/// <remarks>
/// Both overrides deliberately drop the base's <c>= default</c> on the cancellation token.
/// That is the mistake <see cref="GeneratedControllerBindingTests"/> documents, reproduced
/// here so the reflection assertion has something real to observe.
/// </remarks>
public abstract class ProbeControllerBase : DmrpControllerBase
{
    public override Task<ActionResult<ReportingPlanResponse>> GetMonthlyMedicineReportingPlan(
        string facilityId, int reportingMonth, int reportingYear, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ReportingPlanResponse>>(new ReportingPlanResponse
        {
            FacilityId = facilityId,
            ReportingMonth = reportingMonth,
            ReportingYear = reportingYear,
            Measures = [],
            RetrievedOn = DateTimeOffset.UnixEpoch
        });
    }

    public override Task<ActionResult<ReportingPlanResponse>> GetPatientSafetyAnnualReportingPlan(
        string facilityId, int reportingYear, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ReportingPlanResponse>>(new ReportingPlanResponse
        {
            FacilityId = facilityId,
            ReportingMonth = null,
            ReportingYear = reportingYear,
            Measures = [],
            RetrievedOn = DateTimeOffset.UnixEpoch
        });
    }
}
