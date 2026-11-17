using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    [Trait("Category", "UnitTests")]
    public class AuditableFilterTests
    {
        private static readonly AuditableAttribute UpdateFacility = new(AuditEventType.Update, "Facility");

        private sealed class Harness
        {
            public Harness(AuditableAttribute? auditable = null,
                RouteValueDictionary? route = null, string? queryString = null)
            {
                Publisher = new FakeAuditPublisher();
                Audit = new AuditContext();
                Filter = new AuditableFilter(auditable ?? UpdateFacility,
                    AuditTest.Recorder(Audit, Publisher));

                var httpContext = new DefaultHttpContext { User = AuditTest.SignedInAs("nurse@example.org") };
                if (queryString is not null)
                {
                    httpContext.Request.QueryString = new QueryString(queryString);
                }

                var routeData = new RouteData(route ?? new RouteValueDictionary { ["id"] = "7c9e6679" });
                ActionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
            }

            public FakeAuditPublisher Publisher { get; }

            public AuditContext Audit { get; }

            public AuditableFilter Filter { get; }

            public ActionContext ActionContext { get; }

            /// <summary>
            /// A whole request: the action stage, then the result stage that writes the response.
            /// </summary>
            public Task RunReturning(IActionResult? result) =>
                RunBothStages(() => Task.FromResult(Executed(executed => executed.Result = result)), result);

            /// <summary>
            /// The action stage alone, for a request whose response is not written yet.
            /// </summary>
            public Task RunActionReturning(IActionResult? result) =>
                RunAction(() => Task.FromResult(Executed(executed => executed.Result = result)));

            public Task RunThrowing(Exception exception) => RunAction(() => throw exception);

            public Task RunHandling(Exception exception) =>
                RunBothStages(() => Task.FromResult(Executed(executed =>
                {
                    executed.Exception = exception;
                    executed.ExceptionHandled = true;
                    executed.Result = new OkResult();
                })), new OkResult());

            public Task RunUnhandled(Exception exception) =>
                RunAction(() => Task.FromResult(Executed(executed => executed.Exception = exception)));

            public Task RunObserving(Action whileTheActionRuns) =>
                RunBothStages(() =>
                {
                    whileTheActionRuns();
                    return Task.FromResult(Executed(executed => executed.Result = new OkResult()));
                }, new OkResult());

            /// <summary>
            /// The result stage, standing in for MVC's result executor: it writes the status the
            /// result carries, or whichever one the test asks for.
            /// </summary>
            public Task RunResultStage(IActionResult result, Action? whileTheResultExecutes = null,
                Exception? exception = null, bool exceptionHandled = false, int? statusCode = null,
                bool canceled = false)
            {
                var executing = new ResultExecutingContext(ActionContext, [], result, controller: new object());

                return Filter.OnResultExecutionAsync(executing, () =>
                {
                    ActionContext.HttpContext.Response.StatusCode = statusCode ?? StatusCodeOf(result);
                    whileTheResultExecutes?.Invoke();

                    var executed = new ResultExecutedContext(ActionContext, [], result, controller: new object())
                    {
                        Canceled = canceled
                    };

                    if (exception is not null)
                    {
                        executed.Exception = exception;
                        executed.ExceptionHandled = exceptionHandled;
                    }

                    return Task.FromResult(executed);
                });
            }

            private async Task RunBothStages(ActionExecutionDelegate next, IActionResult? result)
            {
                await RunAction(next);

                //MVC turns an action that produced nothing into an EmptyResult and executes that.
                await RunResultStage(result ?? new EmptyResult());
            }

            private Task RunAction(ActionExecutionDelegate next)
            {
                var executing = new ActionExecutingContext(ActionContext, [],
                    new Dictionary<string, object?>(), controller: new object());

                return Filter.OnActionExecutionAsync(executing, next);
            }

            private static int StatusCodeOf(IActionResult result) =>
                result is IStatusCodeActionResult { StatusCode: { } code } ? code : StatusCodes.Status200OK;

            private ActionExecutedContext Executed(Action<ActionExecutedContext> configure)
            {
                var executed = new ActionExecutedContext(ActionContext, [], controller: new object());
                configure(executed);
                return executed;
            }
        }

        [Fact]
        public async Task AnAuditedRequest_IsRecordingWhileTheActionRuns()
        {
            var harness = new Harness();
            var recording = false;

            await harness.RunObserving(() => recording = harness.Audit.IsRecording);

            //the interceptor captures nothing until this is set, so the action's saves depend on it.
            Assert.True(recording);
        }

        [Fact]
        public async Task ASuccessfulAction_PublishesExactlyOneEvent()
        {
            var harness = new Harness();

            await harness.RunReturning(new OkResult());

            var published = Assert.Single(harness.Publisher.Published);
            Assert.Equal(AuditEventType.Update, published.Action);
            Assert.Equal("nurse@example.org", published.User);
            Assert.Equal("7c9e6679", published.ResourceId);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(404)]
        [InlineData(409)]
        [InlineData(500)]
        public async Task AnErrorResponse_PublishesNothing(int statusCode)
        {
            var harness = new Harness();

            await harness.RunReturning(new StatusCodeResult(statusCode));

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AProblemResult_PublishesNothing()
        {
            var harness = new Harness();

            //the shape the REST guidance asks for on a validation failure.
            await harness.RunReturning(new BadRequestObjectResult(new ProblemDetails { Status = 400 }));

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AnActionThatThrows_PublishesNothingAndLetsTheExceptionOut()
        {
            var harness = new Harness();
            var boom = new InvalidOperationException("the handler failed");

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunThrowing(boom));

            Assert.Same(boom, thrown);
            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AnUnhandledExceptionOnTheResult_PublishesNothing()
        {
            var harness = new Harness();

            await harness.RunUnhandled(new InvalidOperationException("the handler failed"));

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AnExceptionAFilterHandledIntoASuccess_IsAudited()
        {
            var harness = new Harness();

            //the request succeeded as far as the caller is concerned, so the change stands.
            await harness.RunHandling(new InvalidOperationException("handled upstream"));

            Assert.Single(harness.Publisher.Published);
        }

        [Fact]
        public async Task AnActionThatProducedNoResult_PublishesNothing()
        {
            var harness = new Harness();

            await harness.RunReturning(null);

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task TheFacility_IsReadFromTheQueryStringWhenTheRouteHasNone()
        {
            var harness = new Harness(queryString: "?facilityId=TestFacility01");

            await harness.RunReturning(new OkResult());

            Assert.Equal("TestFacility01", Assert.Single(harness.Publisher.Published).FacilityId);
        }

        [Fact]
        public async Task TheFacility_IsReadFromTheRouteKeyTheAttributeNames()
        {
            var auditable = new AuditableAttribute(AuditEventType.Update, "SftpConfiguration")
            {
                FacilityIdRouteKey = "organizationId"
            };
            var harness = new Harness(auditable: auditable,
                route: new RouteValueDictionary { ["organizationId"] = "TestFacility01" });

            await harness.RunReturning(new OkResult());

            Assert.Equal("TestFacility01", Assert.Single(harness.Publisher.Published).FacilityId);
        }

        [Fact]
        public async Task TheEvent_IsNotPublishedUntilTheResultHasExecuted()
        {
            var harness = new Harness();
            var publishedWhileTheResultWasStillExecuting = -1;

            await harness.RunActionReturning(new OkResult());

            //the action has returned and MVC has not executed its result yet, so nothing can have
            //described the request as a success.
            Assert.Empty(harness.Publisher.Published);

            await harness.RunResultStage(new OkResult(),
                whileTheResultExecutes: () =>
                    publishedWhileTheResultWasStillExecuting = harness.Publisher.Published.Count);

            Assert.Equal(0, publishedWhileTheResultWasStillExecuting);
            Assert.Single(harness.Publisher.Published);
        }

        [Fact]
        public async Task AResultThatFailsToExecute_PublishesNothing()
        {
            var harness = new Harness();
            await harness.RunActionReturning(new OkResult());

            //the action succeeded; serialising its response did not, so the caller's request failed.
            await harness.RunResultStage(new OkResult(),
                exception: new InvalidOperationException("a possible object cycle was detected"));

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AResultWhoseFailureAFilterSwallowed_PublishesNothing()
        {
            var harness = new Harness();
            await harness.RunActionReturning(new OkResult());

            //handling an exception in the result stage only stops it being rethrown: no result is
            //substituted and nothing re-executes, so the caller still has a broken response.
            await harness.RunResultStage(new OkResult(),
                exception: new InvalidOperationException("a possible object cycle was detected"),
                exceptionHandled: true);

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AResultThatWritesAnErrorStatus_PublishesNothing()
        {
            var harness = new Harness();
            await harness.RunActionReturning(new OkResult());

            //a result object that says 200 but writes something else: only the written status counts.
            await harness.RunResultStage(new OkResult(), statusCode: StatusCodes.Status500InternalServerError);

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task AResultAnotherFilterCancelled_PublishesNothing()
        {
            var harness = new Harness();
            await harness.RunActionReturning(new OkResult());

            await harness.RunResultStage(new OkResult(), canceled: true);

            Assert.Empty(harness.Publisher.Published);
        }

        [Fact]
        public async Task ARequestShortCircuitedBeforeTheAction_PublishesNothing()
        {
            var harness = new Harness();

            //authorization, or [ApiController]'s model-state filter: the action stage never runs,
            //while the always-run result stage still does.
            await harness.RunResultStage(new OkResult());

            Assert.Empty(harness.Publisher.Published);
        }
    }
}
