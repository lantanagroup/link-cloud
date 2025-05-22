using DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;

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
