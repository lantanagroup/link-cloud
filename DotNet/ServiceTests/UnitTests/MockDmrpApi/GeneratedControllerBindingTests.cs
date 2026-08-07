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
/// <item>Optional string filters are typed non-nullable. NSwag emits <c>string name</c>, not
/// <c>string?</c>, and [ApiController] treats a non-nullable reference parameter as required —
/// so a filter the contract documents as optional is mandatory in practice until the override
/// restates it.</item>
/// </list>
/// <para>
/// The fourth point is why <c>DmrpController</c> restates <c>name</c>, <c>year</c> and
/// <c>month</c> as nullable rather than taking the generated signature. Both probe controllers
/// below are deliberately wrong in that respect so the failure is observable; only
/// <c>CorrectProbeController</c> shows the shape an implementation should use.
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

        (await client.GetAsync("/annotated/msc?nhsnorgid=100&name=HOB&year=2020&month=2"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync("/annotated/ps/annual?nhsnorgid=100&name=HAI&year=2020&month=2"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithApiController_QueryParametersStillBind()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync(
            "/annotated/msc?nhsnorgid=100&name=HOB&year=2020&month=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Orgid.Should().Be(100);
        plan.Month.Should().Be(2);
        plan.Year.Should().Be(2020);
    }

    [Theory]
    [InlineData("/annotated/msc?name=HOB&year=2020&month=2")]
    [InlineData("/annotated/ps/annual?name=HAI&year=2020&month=2")]
    public async Task WithApiController_OmittingTheRequiredParameterIsRejected(string url)
    {
        // [BindRequired] lives on the base's nhsnorgid parameter and is never restated by the
        // override, so this is the proof that validation attributes inherit as well as
        // binding sources do.
        using var host = await StartHostAsync();

        (await host.GetTestClient().GetAsync(url)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "{0} omits nhsnorgid", url);
    }

    [Fact]
    public async Task WithApiController_OmittingAnOptionalFilterIsRejected()
    {
        // The trap, live again now that the contract has optional filters. NSwag emits
        // `string name` -- not `string?` -- and under an enabled nullable context
        // [ApiController] treats a non-nullable reference parameter as required. So filters
        // the contract documents as optional are mandatory in practice, and omitting one is a
        // 400 rather than "do not narrow by this".
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/annotated/msc?nhsnorgid=100");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadAsStringAsync();
        _output.WriteLine(problem);
        problem.Should().Contain("name");
    }

    [Fact]
    public async Task WhenTheOverrideRestatesNullability_OptionalFiltersAreReallyOptional()
    {
        // The shape a real implementation must use, and what DmrpController does.
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/correct/msc?nhsnorgid=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Orgid.Should().Be(100);
        plan.Month.Should().BeNull("no month was supplied");
        plan.Year.Should().BeNull("no year was supplied");
    }

    [Fact]
    public async Task WhenTheOverrideRestatesNullability_SuppliedFiltersStillBind()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient()
            .GetAsync("/correct/msc?nhsnorgid=100&name=HOB&year=2020&month=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.Orgid.Should().Be(100);
        plan.Month.Should().Be(2);
        plan.Year.Should().Be(2020);
        plan.Plans.Single().Name.Should().Be("HOB");
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
            "/plain/msc?nhsnorgid=100&name=HOB&year=2020&month=2");

        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();

        _output.WriteLine($"without [ApiController], bound NhsnOrgId = '{plan?.Orgid}'");
        plan!.Orgid.Should().Be(100, "the generated [FromQuery] is still honoured");
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
/// The shape a real implementation must use: every optional filter restated as nullable.
/// </summary>
/// <remarks>
/// Not inherited from the generated base. Leaving a filter non-nullable makes
/// <c>[ApiController]</c> treat it as required and reject requests the contract says are
/// valid.
/// </remarks>
[ApiController]
[Route("correct")]
public class CorrectProbeController : ProbeControllerBase
{
    public override Task<ActionResult<ReportingPlanResponse>> GetMonthlyMedicineReportingPlan(
        string nhsnorgid,
        string? name,
        string? year,
        string? month,
        CancellationToken cancellationToken = default)
    {
        return base.GetMonthlyMedicineReportingPlan(nhsnorgid, name!, year!, month!, cancellationToken);
    }
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
        string nhsnorgid, string name, string year, string month, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ReportingPlanResponse>>(Echo(nhsnorgid, name, year, month));
    }

    public override Task<ActionResult<ReportingPlanResponse>> GetPatientSafetyAnnualReportingPlan(
        string nhsnorgid, string name, string year, string month, CancellationToken cancellationToken)
    {
        // Annual: the month never narrows the result, so it is not echoed either.
        return Task.FromResult<ActionResult<ReportingPlanResponse>>(Echo(nhsnorgid, name, year, month: null));
    }

    private static ReportingPlanResponse Echo(string nhsnorgid, string? name, string? year, string? month) =>
        new()
        {
            Orgid = int.TryParse(nhsnorgid, out var org) ? org : null,
            Month = int.TryParse(month, out var m) ? m : null,
            Year = int.TryParse(year, out var y) ? y : null,
            Plans = string.IsNullOrEmpty(name)
                ? []
                : [new ReportingPlanItem { Name = name, Nhsnorgid = nhsnorgid, Reporting = "Y" }]
        };
}
