using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration.CreatePatientEvent;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Integration.CreateReportScheduled;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Interfaces;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Integration;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints
{
    public class IntegrationTestingEndpoints : IApi
    {
        private readonly ILogger<IntegrationTestingEndpoints> _logger;
        private readonly ICreatePatientEvent _createPatientEvent;
        private readonly ICreateReportScheduled _createReportScheduled;

        public IntegrationTestingEndpoints(ILogger<IntegrationTestingEndpoints> logger, ICreatePatientEvent createPatientEvent, ICreateReportScheduled createReportScheduled)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _createPatientEvent = createPatientEvent ?? throw new ArgumentNullException(nameof(createPatientEvent));
            _createReportScheduled = createReportScheduled ?? throw new ArgumentNullException(nameof(createReportScheduled));
        }

        public void RegisterEndpoints(WebApplication app)
        {
            var integrationEndpoints = app.MapGroup("/api/integration")
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Integration" } }
                });

            integrationEndpoints.MapPost("/patient-event", CreatePatientEvent)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Integration Testing - Produce Patient Event",
                    Description = "Produces a new patient event that will be sent to the broker. Allows for testing processes outside of scheduled events."
                });

            integrationEndpoints.MapPost("/report-scheduled", CreateReportScheduled)
                .WithOpenApi(x => new OpenApiOperation(x)
                {
                    Summary = "Integration Testing - Produce Report Scheduled Event",
                    Description = "Produces a new report scheduled event that will be sent to the broker. Allows for testing processes outside of scheduled events."
                });

        }

        public async Task<IResult> CreatePatientEvent(HttpContext context, PatientEvent model)
        {
            var correlationId = await _createPatientEvent.Execute(model);
            return Results.Ok(new { 
                Id = correlationId,
                Message = $"The patient event was created succcessfully with a correlation id of '{correlationId}'."
            });
        }

        public async Task<IResult> CreateReportScheduled(HttpContext context, ReportScheduled model)
        {
            var correlationId = await _createReportScheduled.Execute(model);
            return Results.Ok(new
            {
                Id = correlationId,
                Message = $"The report scheduled event was created succcessfully with a correlation id of '{correlationId}'."
            });
        }
    }
}
