namespace LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;

public record ParameterFactoryResult(string key, string value, bool paged = false, List<string[]>? values = null);

