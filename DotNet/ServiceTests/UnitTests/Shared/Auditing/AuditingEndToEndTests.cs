using LantanaGroup.Link.Shared.Application.Auditing;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Auditing
{
    /// <summary>
    /// The attribute, the filter, a real transaction, the interceptor, the factory and a publisher
    /// that serialises - everything but Kafka itself. These are the two guarantees that cannot be
    /// shown by testing one piece: that a secret never leaves the service, and that a failed request
    /// leaves neither a change nor an event behind.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public class AuditingEndToEndTests : IDisposable
    {
        private static readonly AuditableAttribute UpdateVendor = new(AuditEventType.Update, "Vendor");

        private readonly SqliteConnection _connection;
        private readonly AuditTestDbContext _context;
        private readonly AuditContext _audit = new();
        private readonly SerialisingAuditPublisher _publisher = new();
        private readonly AuditableFilter _filter;
        private readonly ActionContext _actionContext;

        public AuditingEndToEndTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var interceptor = new AuditPropertyChangeInterceptor(_audit, AuditTest.Options().AsOptions(),
                NullLogger<AuditPropertyChangeInterceptor>.Instance);

            _context = new AuditTestDbContext(new DbContextOptionsBuilder<AuditTestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptor)
                .Options);
            _context.Database.EnsureCreated();

            _filter = new AuditableFilter(UpdateVendor,
                AuditTest.Recorder(_audit, _publisher));

            var httpContext = new DefaultHttpContext { User = AuditTest.SignedInAs("nurse@example.org") };
            _actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        }

        /// <summary>
        /// A whole request the way MVC runs one: the action, then the execution of its result. The
        /// event is published in the second stage, so a test that stops after the first sees none.
        /// </summary>
        private async Task RunAction(Func<Task<IActionResult>> handler, Exception? resultFails = null)
        {
            var executing = new ActionExecutingContext(_actionContext, [],
                new Dictionary<string, object?>(), controller: new object());

            IActionResult? result = null;
            await _filter.OnActionExecutionAsync(executing, async () =>
            {
                result = await handler();
                return new ActionExecutedContext(_actionContext, [], controller: new object())
                {
                    Result = result
                };
            });

            var executed = result ?? new EmptyResult();
            var resultExecuting = new ResultExecutingContext(_actionContext, [], executed, controller: new object());

            await _filter.OnResultExecutionAsync(resultExecuting, () =>
            {
                _actionContext.HttpContext.Response.StatusCode = executed is IStatusCodeActionResult
                {
                    StatusCode: { } code
                }
                    ? code
                    : StatusCodes.Status200OK;

                var resultExecuted = new ResultExecutedContext(_actionContext, [], executed, controller: new object());
                if (resultFails is not null)
                {
                    resultExecuted.Exception = resultFails;
                }

                return Task.FromResult(resultExecuted);
            });
        }

        [Fact]
        public async Task AMaskedValue_ReachesTheSerialisedEventOnlyAsTheMask()
        {
            var vendor = new TestVendor
            {
                Name = "Epic",
                ClientSecret = "sk-live-do-not-leak",
                ContactEmail = "admin@example.org"
            };
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();
            _audit.PropertyChanges.Clear();

            await RunAction(async () =>
            {
                vendor.Name = "Cerner";
                vendor.ClientSecret = "sk-live-rotated";
                vendor.ContactEmail = "someone.else@example.org";
                await _context.SaveChangesAsync();
                return new OkResult();
            });

            var json = Assert.Single(_publisher.PublishedJson);
            Assert.DoesNotContain("sk-live-do-not-leak", json);
            Assert.DoesNotContain("sk-live-rotated", json);
            Assert.DoesNotContain("admin@example.org", json);
            Assert.DoesNotContain("someone.else@example.org", json);

            //that they changed is kept; what they changed from and to is not.
            Assert.Contains(nameof(TestVendor.ClientSecret), json);
            Assert.Contains(nameof(TestVendor.ContactEmail), json);
            Assert.Contains("[redacted]", json);

            //what the event does carry is the change a reader of the audit trail needs.
            Assert.Contains("Cerner", json);
        }

        [Fact]
        public async Task AHandlerThatSavesThenFails_PublishesNothingAndItsSaveStands()
        {
            var vendor = new TestVendor { Name = "Epic" };
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            await RunAction(async () =>
            {
                vendor.Name = "Cerner";
                await _context.SaveChangesAsync();
                return new BadRequestObjectResult(new ProblemDetails { Status = 400 });
            });

            //the action did not succeed, so nothing describes it.
            Assert.Empty(_publisher.PublishedJson);

            //the save was the handler's, and so is undoing it: a handler that can fail after saving
            //needs a transaction of its own.
            _context.ChangeTracker.Clear();
            var reloaded = await _context.Vendors.SingleAsync(v => v.Id == vendor.Id);
            Assert.Equal("Cerner", reloaded.Name);
        }

        [Fact]
        public async Task AResponseThatFailsToSerialise_PublishesNothingAndItsSaveStands()
        {
            var vendor = new TestVendor { Name = "Epic" };
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();
            _audit.PropertyChanges.Clear();

            //the action succeeded and committed; writing its response is what failed, so the caller
            //got an error and nothing may claim otherwise.
            await RunAction(async () =>
            {
                vendor.Name = "Cerner";
                await _context.SaveChangesAsync();
                return new OkObjectResult(vendor);
            }, resultFails: new InvalidOperationException("a possible object cycle was detected"));

            Assert.Empty(_publisher.PublishedJson);

            //as with a handler that fails after saving, undoing the save is the handler's own job.
            _context.ChangeTracker.Clear();
            var reloaded = await _context.Vendors.SingleAsync(v => v.Id == vendor.Id);
            Assert.Equal("Cerner", reloaded.Name);
        }

        [Fact]
        public async Task ASuccessfulUpdate_CarriesThePropertyChangesTheInterceptorFound()
        {
            var vendor = new TestVendor { Name = "Epic" };
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();
            _audit.PropertyChanges.Clear();

            await RunAction(async () =>
            {
                vendor.Name = "Cerner";
                await _context.SaveChangesAsync();
                return new OkResult();
            });

            var json = Assert.Single(_publisher.PublishedJson);
            Assert.Contains("\"PropertyName\":\"Name\"", json);
            Assert.Contains("\"InitialPropertyValue\":\"Epic\"", json);
            Assert.Contains("\"NewPropertyValue\":\"Cerner\"", json);

            //the committed change really is in the database.
            _context.ChangeTracker.Clear();
            var reloaded = await _context.Vendors.SingleAsync(v => v.Id == vendor.Id);
            Assert.Equal("Cerner", reloaded.Name);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
