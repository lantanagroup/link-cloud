using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IResult = Microsoft.AspNetCore.Http.IResult;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// Both filters driven by a real pipeline instead of by hand, for the two things the hand-driven
    /// tests cannot show.
    /// </summary>
    /// <remarks>
    /// The first is the wiring each design depends on. <see cref="AuditableFilter"/> carries what the
    /// action stage decided over to the result stage on the instance, so MVC must visit one instance
    /// in both pipelines; if it built a second one for the result stage, every audited controller
    /// endpoint would silently stop publishing and no hand-driven test would notice, because they
    /// construct the instance themselves. <see cref="AuditableEndpointFilter"/> returns a result of
    /// its own for the request delegate to execute, so .NET must execute it rather than discard it. A
    /// successful request publishing exactly one event is that proof on both sides.
    /// <para>
    /// The second is the fault the two of them exist to close: a result that throws while being
    /// written, which neither filter could see before it hooked result execution.
    /// </para>
    /// </remarks>
    [Trait("Category", "UnitTests")]
    public class AuditWiringTests : IAsyncLifetime
    {
        private readonly FakeAuditPublisher _publisher = new();
        private WebApplication _app = null!;
        private HttpClient _client = null!;

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(AuditWiringTests).Assembly);

            //everything AddLinkAuditing registers except the Kafka publisher and the counter, which
            //are faked, and the startup logger, which needs none of this.
            builder.Services.AddSingleton(AuditTest.Service());
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IAuditPublisher>(_publisher);
            builder.Services.AddSingleton<IAuditMetrics>(new FakeAuditMetrics());
            builder.Services.AddScoped<IAuditContext, AuditContext>();
            builder.Services.AddScoped<IAuditEventFactory, AuditEventFactory>();
            builder.Services.AddScoped<AuditRecorder>();

            _app = builder.Build();
            _app.MapControllers();

            _app.MapPut("/probe-endpoint/{id}", (string id) => Results.Ok(new { id }))
                .Auditable(AuditEventType.Update, "WiringProbe");

            _app.MapPut("/probe-endpoint/{id}/failing-result", (string id) => new ThrowingResult())
                .Auditable(AuditEventType.Update, "WiringProbe");

            _app.MapPut("/probe-endpoint/{id}/bad-request",
                    (string id) => Results.BadRequest(new ProblemDetails { Status = 400 }))
                .Auditable(AuditEventType.Update, "WiringProbe");

            await _app.StartAsync();
            _client = _app.GetTestClient();
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _app.DisposeAsync();
        }

        [Theory]
        [InlineData("/wiring-probe/7c9e6679")]
        [InlineData("/probe-endpoint/7c9e6679")]
        public async Task ASuccessfulRequest_PublishesExactlyOneEvent(string url)
        {
            var response = await _client.PutAsync(url, content: null);

            Assert.True(response.IsSuccessStatusCode);

            //one event, which is only possible if the filter really did get to publish after the
            //response was written.
            var published = Assert.Single(_publisher.Published);
            Assert.Equal(AuditEventType.Update, published.Action);
            Assert.Equal("WiringProbe", published.Resource);
            Assert.Equal("7c9e6679", published.ResourceId);
        }

        [Theory]
        [InlineData("/wiring-probe/7c9e6679/failing-result")]
        [InlineData("/probe-endpoint/7c9e6679/failing-result")]
        public async Task ARequestWhoseResultThrowsWhileExecuting_PublishesNothing(string url)
        {
            //the handler succeeded and its result was accepted; writing the response is what failed.
            //How the host reports that is its own business - that no event claims success is not.
            await Record.ExceptionAsync(() => _client.PutAsync(url, content: null));

            Assert.Empty(_publisher.Published);
        }

        [Theory]
        [InlineData("/wiring-probe/7c9e6679/bad-request")]
        [InlineData("/probe-endpoint/7c9e6679/bad-request")]
        public async Task ARequestThatFails_PublishesNothing(string url)
        {
            var response = await _client.PutAsync(url, content: null);

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(_publisher.Published);
        }

        /// <summary>
        /// A minimal-API result that fails the way a serialiser does: once the filter pipeline has
        /// unwound and the request delegate is writing the response.
        /// </summary>
        private sealed class ThrowingResult : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext) =>
                throw new InvalidOperationException("writing the response failed");
        }
    }

    /// <summary>
    /// Stands in for a real controller, so the attribute and the filter are wired by MVC itself.
    /// </summary>
    [Route("wiring-probe")]
    public class WiringProbeController : ControllerBase
    {
        [HttpPut("{id}")]
        [Auditable(AuditEventType.Update, "WiringProbe")]
        public IActionResult Put(string id) => Ok(new { id });

        [HttpPut("{id}/failing-result")]
        [Auditable(AuditEventType.Update, "WiringProbe")]
        public IActionResult PutFailingResult(string id) => new ThrowingResult();

        [HttpPut("{id}/bad-request")]
        [Auditable(AuditEventType.Update, "WiringProbe")]
        public IActionResult PutBadRequest(string id) => BadRequest(new ProblemDetails { Status = 400 });

        /// <summary>
        /// A result that fails the way a serialiser does: after the action has returned, inside
        /// MVC's result execution, where an action filter has already finished.
        /// </summary>
        private sealed class ThrowingResult : IActionResult
        {
            public Task ExecuteResultAsync(ActionContext context) =>
                throw new InvalidOperationException("writing the response failed");
        }
    }
}
