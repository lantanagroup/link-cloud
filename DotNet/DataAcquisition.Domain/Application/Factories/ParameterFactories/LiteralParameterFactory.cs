using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;

public class LiteralParameterFactory
{
    private static readonly ILogger<LiteralParameterFactory> _logger;

    static LiteralParameterFactory()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<LiteralParameterFactory>();
    }

    public static ParameterFactoryResult? Build(LiteralParameter parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name) || string.IsNullOrWhiteSpace(parameter.Literal))
        {
            _logger.LogWarning("LiteralParameter validation failed: Name or Literal is null or whitespace. Name: {Name}, Literal: {Literal}", parameter.Name, parameter.Literal);
            return null;
        }

        return new ParameterFactoryResult(parameter.Name, parameter.Literal);
    }
}
