using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    [Trait("Category", "UnitTests")]
    public class AuditableEndpointFilterTests
    {
        private sealed class Harness
        {
            public Harness(AuditableEndpointFilter? filter = null,
                string? queryString = null, string? organizationId = null)
            {
                Publisher = new FakeAuditPublisher();
                Audit = new AuditContext();

                var services = new ServiceCollection();
                services.AddSingleton(AuditTest.Recorder(Audit, Publisher));

                //the results from Results and TypedResults resolve a logger, and some of them the
                //host's JSON options, while executing.
                services.AddLogging();
                services.AddOptions();

                var httpContext = new DefaultHttpContext
                {
                    User = AuditTest.SignedInAs("nurse@example.org"),
                    RequestServices = services.BuildServiceProvider()
                };
                httpContext.Response.Body = new MemoryStream();
                httpContext.Request.RouteValues["id"] = "7c9e6679";
                if (queryString is not null)
                {
                    httpContext.Request.QueryString = new QueryString(queryString);
                }

                if (organizationId is not null)
                {
                    httpContext.Request.RouteValues["organizationId"] = organizationId;
                }

                Context = new DefaultEndpointFilterInvocationContext(httpContext);
                Filter = filter ?? new AuditableEndpointFilter(AuditEventType.SoftDelete, "LinkUser");
            }

            public FakeAuditPublisher Publisher { get; }

            public AuditContext Audit { get; }

            public AuditableEndpointFilter Filter { get; }

            public DefaultEndpointFilterInvocationContext Context { get; }

            public HttpContext HttpContext => Context.HttpContext;

            /// <summary>
            /// A whole request: the filter pipeline, then the request delegate executing whatever
            /// result came back out of it, which is where the event is published.
            /// </summary>
            public async Task<object?> RunReturning(object? result)
            {
                var returned = await RunWithoutExecuting(result);

                if (returned is IResult executable)
                {
                    await executable.ExecuteAsync(HttpContext);
                }

                return returned;
            }

            /// <summary>
            /// The filter pipeline alone, for a request whose result is not executed yet.
            /// </summary>
            public ValueTask<object?> RunWithoutExecuting(object? result) =>
                Filter.InvokeAsync(Context, _ => ValueTask.FromResult(result));

            public ValueTask<object?> RunThrowing(Exception exception) =>
                Filter.InvokeAsync(Context, _ => throw exception);
        }

        /// <summary>
        /// A result that fails the way a serialiser does: after the handler has returned, while the
        /// request delegate is writing the response, where the filter has already finished.
        /// </summary>
        private sealed class ThrowingResult : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext) =>
                throw new InvalidOperationException("writing the response failed");
        }

        /// <summary>
        /// A result that writes a status of its own, which nothing can know before it executes.
        /// </summary>
        private sealed class StatusWritingResult(int statusCode) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task ASuccessfulHandler_PublishesExactlyOneEvent()
        {
            var harness = new Harness();

            await harness.RunReturning(Results.NoContent());

            var published = Assert.Single(harness.Publisher.Published);
            Assert.Equal(AuditEventType.SoftDelete, published.Action);
            Assert.Equal("LinkUser", published.Resource);
            Assert.Equal("nurse@example.org", published.User);
            Assert.Equal("7c9e6679", published.ResourceId);
        }

        [Fact]
        public async Task APlainValueReturnedAsJson_IsRecordedOnTheWayOut()
        {
            var harness = new Harness();

            //one of the two cases the filter cannot defer: the request delegate serialises a plain
            //value itself, in a way this filter cannot reproduce, so the event is published on the
            //way out as it always was. No audited endpoint returns one today.
            var returned = await harness.RunWithoutExecuting(new { Id = "7c9e6679" });

            Assert.Single(harness.Publisher.Published);
            Assert.IsNotAssignableFrom<IResult>(returned);
        }

        [Fact]
        public async Task AHandlerReturningNothing_IsRecordedOnTheWayOut()
        {
            var harness = new Harness();

            //the other: null is not an IResult either, so there is nothing for the filter to wrap.
            var returned = await harness.RunWithoutExecuting(result: null);

            Assert.Single(harness.Publisher.Published);
            Assert.Null(returned);
        }

        [Fact]
        public async Task TheHandlersResult_IsTheOneTheCallerGets()
        {
            var harness = new Harness();

            //the filter returns a result of its own so that it can publish after the response is
            //written, but what reaches the caller is still exactly what the handler returned.
            await harness.RunReturning(Results.NoContent());

            Assert.Equal(StatusCodes.Status204NoContent, harness.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task TheEvent_IsNotPublishedUntilTheHandlersResultHasExecuted()
        {
            var harness = new Harness();

            var returned = await harness.RunWithoutExecuting(Results.NoContent());

            //the handler has returned and the request delegate has not written its result yet, so
            //nothing can have described the request as a success.
            Assert.Empty(harness.Publisher.Published);

            await Assert.IsAssignableFrom<IResult>(returned).ExecuteAsync(harness.HttpContext);

            Assert.Single(harness.Publisher.Published);
        }

        [Fact]
        public async Task AResultThatThrowsWhileExecuting_PublishesNothing()
        {
            var harness = new Harness();

            //the handler succeeded; writing its response did not, so the caller's request failed.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.RunReturning(new ThrowingResult()));

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AResultThatWritesAnErrorStatus_PublishesNothing()
        {
            var harness = new Harness();

            await harness.RunReturning(new StatusWritingResult(StatusCodes.Status500InternalServerError));

            Assert.Empty(harness.Publisher.Published);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(404)]
        [InlineData(409)]
        public async Task AnErrorResult_PublishesNothing(int statusCode)
        {
            var harness = new Harness();

            await harness.RunReturning(Results.StatusCode(statusCode));

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AProblemResult_PublishesNothing()
        {
            var harness = new Harness();

            await harness.RunReturning(Results.Problem("the id was not a guid", statusCode: 400));

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AHandlerThatThrows_PublishesNothingAndLetsTheExceptionOut()
        {
            var harness = new Harness();
            var boom = new InvalidOperationException("the handler failed");

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await harness.RunThrowing(boom));

            Assert.Same(boom, thrown);
            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task TheHandlerCanOverrideTheActionForACreateOrUpdateEndpoint()
        {
            var harness = new Harness();
            harness.Audit.ActionOverride = AuditEventType.Create;

            await harness.RunReturning(Results.Created("/user/7c9e6679", null));

            Assert.Equal(AuditEventType.Create, Assert.Single(harness.Publisher.Published).Action);
        }

        [Fact]
        public async Task TheFacility_IsReadFromTheQueryStringWhenTheRouteHasNone()
        {
            var harness = new Harness(queryString: "?facilityId=TestFacility01");

            await harness.RunReturning(Results.NoContent());

            Assert.Equal("TestFacility01", Assert.Single(harness.Publisher.Published).FacilityId);
        }

        [Fact]
        public async Task TheFacility_IsReadFromTheRouteKeyTheEndpointNames()
        {
            var harness = new Harness(
                filter: new AuditableEndpointFilter(AuditEventType.Update, "SftpConfiguration",
                    facilityIdRouteKey: "organizationId"),
                organizationId: "TestFacility01");

            await harness.RunReturning(Results.NoContent());

            Assert.Equal("TestFacility01", Assert.Single(harness.Publisher.Published).FacilityId);
        }

        [Fact]
        public async Task PropertyChanges_AreCarriedOnAnUpdate()
        {
            var harness = new Harness(filter: new AuditableEndpointFilter(AuditEventType.Update, "LinkUser"));
            harness.Audit.PropertyChanges.Add(new("Role", "Viewer", "Admin"));

            await harness.RunReturning(Results.NoContent());

            Assert.Single(Assert.Single(harness.Publisher.Published).PropertyChanges!);
        }

        [Fact]
        public async Task PropertyChanges_AreLeftOffWhenTheEndpointSuppressesThem()
        {
            var harness = new Harness(filter: new AuditableEndpointFilter(AuditEventType.Update, "LinkUser",
                suppressPropertyChanges: true));
            harness.Audit.PropertyChanges.Add(new("Role", "Viewer", "Admin"));

            await harness.RunReturning(Results.NoContent());

            Assert.Null(Assert.Single(harness.Publisher.Published).PropertyChanges);
        }
    }
}
