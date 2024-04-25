using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using LantanaGroup.Link.Normalization.Domain.Entities;
using MediatR;

namespace LantanaGroup.Link.Normalization.Application.Commands.Config;

public class DeleteConfigCommand : IRequest
{
    public string FacilityId { get; set; }
}

public class DeleteConfigCommandHandler : IRequestHandler<DeleteConfigCommand>
{
    private readonly ILogger<DeleteConfigCommandHandler> _logger;
    private readonly NormalizationDbContext _dbContext;

    public DeleteConfigCommandHandler(ILogger<DeleteConfigCommandHandler> logger, NormalizationDbContext configRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    }

    public async Task Handle(DeleteConfigCommand request, CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new ConfigOperationNullException();
        }

        _dbContext.Remove(request.FacilityId);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
