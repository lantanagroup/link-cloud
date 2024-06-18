using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryConfig;

public class GetFhirQueryConfigQuery : IRequest<FhirQueryConfiguration>
{
    public string FacilityId { get; set; }
}

public class GetFhirQueryConfigQueryHandler : IRequestHandler<GetFhirQueryConfigQuery, FhirQueryConfiguration>
{
    private readonly ILogger<GetFhirQueryConfigQueryHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _repository;

    public GetFhirQueryConfigQueryHandler(ILogger<GetFhirQueryConfigQueryHandler> logger, IFhirQueryConfigurationRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Returns the FhirQueryConfiguration for the provided facilityId or throws a NotFoundException if none exists.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="BadRequestException"></exception>
    /// <exception cref="NotFoundException"></exception>
    public async Task<FhirQueryConfiguration> Handle(GetFhirQueryConfigQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new BadRequestException("GetFhirQueryConfigQuery.FacilityId is null or empty.");
        }

        var result = await _repository.GetAsync(request.FacilityId, cancellationToken);

        if (result == null)
        {
            throw new NotFoundException($"No {nameof(FhirQueryConfiguration)} found for facilityId: {request.FacilityId}");
        }

        return result;
    }
}
