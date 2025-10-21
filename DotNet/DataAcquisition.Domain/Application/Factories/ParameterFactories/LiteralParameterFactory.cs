using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;

public class LiteralParameterFactory : ILiteralParameterFactory
{
    private readonly ILogger<LiteralParameterFactory> _logger;

    public LiteralParameterFactory(ILogger<LiteralParameterFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ParameterFactoryResult? Build(LiteralParameter parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name) || string.IsNullOrWhiteSpace(parameter.Literal))
        {
            _logger.LogWarning("LiteralParameter validation failed: Name or Literal is null or whitespace. Name: {Name}, Literal: {Literal}", parameter.Name, parameter.Literal);
            return null;
        }

        return new ParameterFactoryResult(parameter.Name, parameter.Literal);
    }
}
