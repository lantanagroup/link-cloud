using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
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

    public SaveFhirListCommandHandler(ILogger<SaveFhirListCommandHandler> logger, IFhirQueryListConfigurationRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task Handle(SaveFhirListCommand request, CancellationToken cancellationToken)
    {
        
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
