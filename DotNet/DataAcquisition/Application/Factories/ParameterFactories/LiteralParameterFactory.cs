using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories.ParameterFactories;

public class LiteralParameterFactory
{
    public static ParameterFactoryResult Build(LiteralParameter parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name) || string.IsNullOrWhiteSpace(parameter.Literal))
            return null;

        return new ParameterFactoryResult(parameter.Name, parameter.Literal);
    }
}
