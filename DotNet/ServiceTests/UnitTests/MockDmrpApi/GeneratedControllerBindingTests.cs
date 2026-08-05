using System.Net;
using System.Net.Http.Json;
using System.Reflection;
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
/// Three things are worth knowing before implementing against this base, and all three
/// are easier to verify than to reason about -- so these tests drive real HTTP through a
/// real MVC pipeline, and will fail loudly if an NSwag or ASP.NET upgrade changes any of
/// them.
/// <list type="number">
/// <item>Binding source attributes on the generated base <em>do</em> reach the override.
/// MVC resolves them through the base declaration, so [FromQuery] and [FromBody] keep
/// working. Only parameters with no source attribute need [ApiController] inference.</item>
/// <item>Default parameter values do <em>not</em> carry over. An override that omits
/// <c>= 10</c> silently binds null.</item>
/// <item>NSwag types optional string filters as non-nullable <c>string</c>, which
/// [ApiController] treats as required. An override must restate them as <c>string?</c>
/// or the contract's optional filters become mandatory.</item>
/// </list>
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

    // ------------------------------------------------------- the C# level fact

    [Fact]
    public void ParameterAttributesReachTheOverrideOnlyViaTheStaticAttributeHelper()
    {
        // Two reflection APIs disagree, and the difference is why binding still works.
        // ParameterInfo.GetCustomAttributes ignores its inherit flag and reports only the
        // override's own attributes -- none. Attribute.GetCustomAttributes walks to the
        // base method's parameter, and that is the one MVC's behaviour matches.
        var overrideParameter = typeof(AnnotatedProbeController)
            .GetMethod(nameof(AnnotatedProbeController.GetReportingPlan))!
            .GetParameters()[0];

        var viaParameterInfo = overrideParameter
            .GetCustomAttributes(inherit: true).Select(a => a.GetType().Name).ToArray();
        var viaAttributeHelper = Attribute
            .GetCustomAttributes(overrideParameter, inherit: true).Select(a => a.GetType().Name).ToArray();

        _output.WriteLine($"ParameterInfo.GetCustomAttributes(true): [{string.Join(", ", viaParameterInfo)}]");
        _output.WriteLine($"Attribute.GetCustomAttributes(p, true):  [{string.Join(", ", viaAttributeHelper)}]");

        viaParameterInfo.Should().BeEmpty();
        viaAttributeHelper.Should().Contain("FromQueryAttribute");
    }

    [Fact]
    public void DefaultParameterValuesAreNotInheritedByTheOverride()
    {
        // The base declares pageSize = 10. The override redeclares the parameter without
        // a default, and defaults are baked into the declaring signature rather than
        // resolved through the base -- so the generated default is silently lost.
        var baseParameter = typeof(DmrpControllerBase)
            .GetMethod(nameof(DmrpControllerBase.GetReportingPlanEntriesByFacility))!
            .GetParameters().Single(p => p.Name == "pageSize");
        var overrideParameter = typeof(AnnotatedProbeController)
            .GetMethod(nameof(AnnotatedProbeController.GetReportingPlanEntriesByFacility))!
            .GetParameters().Single(p => p.Name == "pageSize");

        _output.WriteLine($"base pageSize default:     {baseParameter.HasDefaultValue} ({baseParameter.RawDefaultValue})");
        _output.WriteLine($"override pageSize default: {overrideParameter.HasDefaultValue}");

        baseParameter.HasDefaultValue.Should().BeTrue();
        overrideParameter.HasDefaultValue.Should().BeFalse(
            "this probe deliberately omits it, which is exactly the mistake to avoid");
    }

    // ------------------------------------------------- what MVC actually does

    [Fact]
    public async Task WithApiController_QueryParametersStillBind()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync(
            "/annotated/reporting-plans?facilityId=F1&reportingMonth=5&reportingYear=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<ReportingPlanResponse>();
        plan!.FacilityId.Should().Be("F1");
        plan.ReportingMonth.Should().Be(5);
        plan.ReportingYear.Should().Be(2026);
    }

    [Fact]
    public async Task WithApiController_JsonBodyStillBinds()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().PostAsJsonAsync("/annotated", new ReportingPlanEntryRequest
        {
            FacilityId = "F1",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var echoed = await response.Content.ReadFromJsonAsync<ReportingPlanEntry>();
        echoed!.FacilityId.Should().Be("F1");
        echoed.Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task WithApiController_RouteAndOptionalQueryParametersStillBind()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient()
            .GetAsync("/annotated/facilities/F9?pageSize=25&pageNumber=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();
        page!.Metadata.PageSize.Should().Be(25);
        page.Metadata.PageNumber.Should().Be(3);
        page.Records.Single().FacilityId.Should().Be("F9");
    }

    [Fact]
    public async Task WithApiController_EnumQueryParametersBindWhenAllStringFiltersAreSupplied()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync(
            "/annotated/search?facilityId=F1&measure=HOB&isReporting=Y&sortBy=Measure&sortOrder=Ascending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();
        page!.Records.Single().Measure.Should().Be("Measure/Ascending");
    }

    [Fact]
    public async Task WithApiController_OmittingAnOptionalStringFilterIsRejected()
    {
        // The trap. NSwag emits `string facilityId` -- not `string?` -- for optional query
        // filters, and under an enabled nullable context [ApiController] treats a
        // non-nullable reference parameter as required. So filters the contract documents
        // as optional are mandatory in practice, and omitting one is a 400.
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/annotated/search?sortBy=Measure");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadAsStringAsync();
        _output.WriteLine(problem);
        problem.Should().Contain("facilityId");
    }

    [Fact]
    public async Task WithApiController_OmittedOptionalParametersLoseTheirDefaultWhenTheOverrideDropsIt()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/annotated/facilities/F9");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();

        // -1 is the probe's stand-in for null. The generated base declares pageSize = 10,
        // but this override redeclared the parameter without a default and MVC therefore
        // binds null -- a paging bug that no compiler warning would catch.
        page!.Metadata.PageSize.Should().Be(-1);
        page.Metadata.PageNumber.Should().Be(-1);
    }

    // --------------------------------- the shape an implementation should use

    [Fact]
    public async Task WhenTheOverrideRestatesDefaultsAndNullability_OptionalParametersBehave()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        // Defaults apply when omitted.
        var byFacility = await client.GetAsync("/correct/facilities/F9");
        byFacility.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await byFacility.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();
        page!.Metadata.PageSize.Should().Be(10);
        page.Metadata.PageNumber.Should().Be(1);

        // Optional string filters really are optional.
        var search = await client.GetAsync("/correct/search?sortBy=Measure");
        search.StatusCode.Should().Be(HttpStatusCode.OK);
        var searched = await search.Content.ReadFromJsonAsync<ReportingPlanEntryPage>();
        searched!.Records.Single().Measure.Should().Be("Measure/Descending");
        searched.Metadata.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task WithApiController_MissingRequiredQueryParameterIsRejected()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/annotated/reporting-plans?facilityId=F1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WithoutApiController_ExplicitBindingSourcesStillApply()
    {
        // [FromBody] on the generated base still reaches MVC without [ApiController],
        // because MVC resolves parameter attributes through the base declaration. What
        // [ApiController] adds is inference for parameters with no source attribute.
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().PostAsJsonAsync("/plain", new ReportingPlanEntryRequest
        {
            FacilityId = "F1",
            Measure = "HOB",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y"
        });

        var echoed = await response.Content.ReadFromJsonAsync<ReportingPlanEntry>();

        _output.WriteLine($"without [ApiController], bound FacilityId = '{echoed?.FacilityId}'");
        echoed!.FacilityId.Should().Be("F1", "the generated [FromBody] is still honoured");
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
/// The shape a real implementation should use: every optional parameter restates its
/// default, and every optional string restates itself as nullable.
/// </summary>
/// <remarks>
/// Neither is inherited from the generated base. Dropping a default silently turns a
/// paged endpoint into an unpaged one; leaving a filter non-nullable makes [ApiController]
/// treat it as required and reject requests the contract says are valid.
/// </remarks>
[ApiController]
[Route("correct")]
public class CorrectProbeController : ProbeControllerBase
{
    public override Task<ActionResult<ReportingPlanEntryPage>> GetReportingPlanEntriesByFacility(
        string facilityId,
        int? pageSize = 10,
        int? pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return base.GetReportingPlanEntriesByFacility(facilityId, pageSize, pageNumber, cancellationToken);
    }

    public override Task<ActionResult<ReportingPlanEntryPage>> SearchReportingPlanEntries(
        string? facilityId,
        string? measure,
        int? reportingMonth,
        int? reportingYear,
        string? isReporting,
        SortBy? sortBy = SortBy.CreateDate,
        SortOrder? sortOrder = SortOrder.Descending,
        int? pageSize = 10,
        int? pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        return base.SearchReportingPlanEntries(
            facilityId!, measure!, reportingMonth, reportingYear, isReporting!,
            sortBy, sortOrder, pageSize, pageNumber, cancellationToken);
    }
}

/// <summary>
/// Echoes back whatever MVC managed to bind, so a test can tell binding success from
/// silent failure. Only the members the tests exercise do anything.
/// </summary>
public abstract class ProbeControllerBase : DmrpControllerBase
{
    public override Task<ActionResult<ReportingPlanResponse>> GetReportingPlan(
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

    public override Task<ActionResult<ReportingPlanEntry>> CreateReportingPlanEntry(
        ReportingPlanEntryRequest body, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ReportingPlanEntry>>(new ReportingPlanEntry
        {
            Id = "probe",
            FacilityId = body?.FacilityId ?? "<null body>",
            Measure = body?.Measure ?? "<null body>",
            ReportingMonth = body?.ReportingMonth ?? 0,
            ReportingYear = body?.ReportingYear ?? 0,
            IsReporting = "Y"
        });
    }

    public override Task<ActionResult<ReportingPlanEntryPage>> GetReportingPlanEntriesByFacility(
        string facilityId, int? pageSize, int? pageNumber, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ReportingPlanEntryPage>>(
            Page(facilityId, "n/a", pageSize, pageNumber));
    }

    public override Task<ActionResult<ReportingPlanEntryPage>> SearchReportingPlanEntries(
        string facilityId, string measure, int? reportingMonth, int? reportingYear, string isReporting,
        SortBy? sortBy, SortOrder? sortOrder, int? pageSize, int? pageNumber,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<ReportingPlanEntryPage>>(
            Page(facilityId ?? "<null>", $"{sortBy}/{sortOrder}", pageSize, pageNumber));
    }

    private static ReportingPlanEntryPage Page(string facilityId, string measure, int? pageSize, int? pageNumber) =>
        new()
        {
            Records =
            [
                new ReportingPlanEntry
                {
                    Id = "probe",
                    FacilityId = facilityId,
                    Measure = measure,
                    ReportingMonth = 1,
                    ReportingYear = 2026,
                    IsReporting = "Y"
                }
            ],
            Metadata = new PageMetadata
            {
                PageSize = pageSize ?? -1,
                PageNumber = pageNumber ?? -1,
                TotalCount = 1,
                TotalPages = 1
            }
        };

    // ---- Not exercised by these probes. ----

    public override Task<ActionResult<ReportingPlanEntry>> GetReportingPlanEntry(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task<ActionResult<ReportingPlanEntry>> UpdateReportingPlanEntry(string id, ReportingPlanEntry body, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task<IActionResult> DeleteReportingPlanEntry(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task<IActionResult> DeleteAllReportingPlanEntries(CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task<IActionResult> DeleteReportingPlanEntriesByFacility(string facilityId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public override Task<ActionResult<AuthTokenResponse>> IssueToken(TokenRequest body, CancellationToken cancellationToken) => throw new NotSupportedException();
}
