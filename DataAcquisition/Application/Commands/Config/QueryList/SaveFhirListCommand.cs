using LantanaGroup.Link.DataAcquisition.Application.Commands.Config.TenantCheck;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.Config.QueryList;

public class SaveFhirListCommand : IRequest
{
    public string FacilityId { get; set; }
    public FhirListConfiguration FhirListConfiguration { get; set; }
}

public class SaveFhirListCommandHandler : IRequestHandler<SaveFhirListCommand>
{
    private readonly ILogger<SaveFhirListCommandHandler> _logger;
    private readonly IFhirQueryListConfigurationRepository _repository;
    private readonly IMediator _mediator;

    public SaveFhirListCommandHandler(ILogger<SaveFhirListCommandHandler> logger, IFhirQueryListConfigurationRepository repository, IMediator mediator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task Handle(SaveFhirListCommand request, CancellationToken cancellationToken)
    {
        if (await _mediator.Send(new CheckIfTenantExistsQuery { TenantId = request.FacilityId }, cancellationToken) == false)
        {
            throw new MissingFacilityConfigurationException($"Facility {request.FacilityId} not found.");
        }

        var config = await _repository.GetByFacilityIdAsync(request.FacilityId);

        if (config == null)
        {
            request.FhirListConfiguration.ModifyDate = DateTime.UtcNow;
            request.FhirListConfiguration.CreateDate = DateTime.UtcNow;
            await _repository.AddAsync(request.FhirListConfiguration);
        }
        else
        {
            request.FhirListConfiguration.Id = config.Id;
            request.FhirListConfiguration.ModifyDate = DateTime.UtcNow;
            request.FhirListConfiguration.CreateDate = config.CreateDate;
            await _repository.UpdateAsync(config);
        }
        
    }
}
