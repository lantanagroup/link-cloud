using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Validate;

public class ValidateFacilityConnectionQuery : IRequest<FacilityConnectionResult>
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? PatientIdentifier { get; set; }
    public string? MeasureId { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
}

public class ValidateFacilityConnectionQueryHandler : IRequestHandler<ValidateFacilityConnectionQuery, FacilityConnectionResult>
{
    private readonly ILogger<ValidateFacilityConnectionQueryHandler> _logger;
    private readonly IMediator _mediator;

    public ValidateFacilityConnectionQueryHandler(
        ILogger<ValidateFacilityConnectionQueryHandler> logger,
        IFhirApiRepository fhirRepo,
        IFhirQueryConfigurationRepository fhirQueryRepo,
        IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<FacilityConnectionResult> Handle(ValidateFacilityConnectionQuery request, CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new MissingFacilityIdException();
        }

        if(string.IsNullOrWhiteSpace(request.PatientId) && string.IsNullOrWhiteSpace(request.PatientIdentifier))
        {
            throw new MissingPatientIdOrPatientIdentifierException();
        }

        if(string.IsNullOrWhiteSpace(request.MeasureId))
        {
            throw new BadRequestException("Missing Measure ID.");
        }

        if(request.Start == null || request.Start == default)
        {
            throw new BadRequestException("Invalid start date.");
        }

        if(request.End == null || request.End == default)
        {
            throw new BadRequestException("Invalid end date.");
        }

        if (!string.IsNullOrWhiteSpace(request.PatientId) && !request.PatientId.StartsWith("Patient/"))
            request.PatientId = $"Patient/{request.PatientId}";

        List<IBaseMessage> intialResults = null;
        List<IBaseMessage> supplementalResults = null;
        try
        {
            intialResults = await _mediator.Send(new GetPatientDataRequest
            {
                FacilityId = request.FacilityId,
                CorrelationId = Guid.NewGuid().ToString(),
                QueryPlanType = QueryPlanType.InitialQueries,
                Message = new DataAcquisitionRequested
                {
                    PatientId = request.PatientId,
                    QueryType = "Initial",
                    Topic = "ConnectionValidation",
                    ScheduledReports = new List<ScheduledReport>
                    {
                        new ScheduledReport
                        {
                            ReportType = request.MeasureId,
                            StartDate = request.Start.ToString(),
                            EndDate = request.End.ToString()
                        }
                    }
                }
            }, cancellationToken);

            supplementalResults = await _mediator.Send(new GetPatientDataRequest
            {
                FacilityId = request.FacilityId,
                CorrelationId = Guid.NewGuid().ToString(),
                QueryPlanType = QueryPlanType.SupplementalQueries,
                Message = new DataAcquisitionRequested
                {
                    PatientId = request.PatientId,
                    QueryType = "Supplemental",
                    Topic = "ConnectionValidation",
                    ScheduledReports = new List<ScheduledReport>
                    {
                        new ScheduledReport
                        {
                            ReportType = request.MeasureId,
                            StartDate = request.Start.ToString(),
                            EndDate = request.End.ToString()
                        }
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex) when (
            ex is FhirConnectionFailedException ||
            ex is FhirOperationException ||
            ex is StructuralTypeException ||
            ex is HttpRequestException)
        {
            _logger.LogError(ex, "Error validating facility connection for facility {FacilityId}", request.FacilityId);
            throw new FhirConnectionFailedException($"Error validating facility connection for facility {request.FacilityId}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating facility connection for facility {FacilityId}. Please check your configuration.", request.FacilityId);
            throw;
        }

        FacilityConnectionResult? result;

        //create bundle
        var bundle = new Bundle();

        //add initial results
        var entries = intialResults.Select(r => new Bundle.EntryComponent { Resource = ((ResourceAcquired)r).Resource });
        bundle.Entry.AddRange(entries);

        //add supplemental results
        entries = supplementalResults.Select(r => new Bundle.EntryComponent { Resource = ((ResourceAcquired)r).Resource });
        bundle.Entry.AddRange(entries);

        return new FacilityConnectionResult(true, true, bundle: bundle);
    }
}
