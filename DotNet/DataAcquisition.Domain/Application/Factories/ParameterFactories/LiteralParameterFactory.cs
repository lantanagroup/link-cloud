using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.ParameterFactories;

public class LiteralParameterFactory
{
    public static ParameterFactoryResult Build(LiteralParameter parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name) || string.IsNullOrWhiteSpace(parameter.Literal))
            return null;

        return new ParameterFactoryResult(parameter.Name, parameter.Literal);
    }
}
