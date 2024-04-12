using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Application.Services;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config;

public class DeleteConfigCommand : IRequest
{
    public string FacilityId { get; set; }
}

public class DeleteConfigCommandHandler : IRequestHandler<DeleteConfigCommand>
{
    private readonly ILogger<DeleteConfigCommandHandler> _logger;
    private readonly IConfigRepository _configRepository;

    public DeleteConfigCommandHandler(ILogger<DeleteConfigCommandHandler> logger, IConfigRepository configRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task Handle(DeleteConfigCommand request, CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new ConfigOperationNullException();
        }

        await _configRepository.DeleteAsync(request.FacilityId);
    }
}
