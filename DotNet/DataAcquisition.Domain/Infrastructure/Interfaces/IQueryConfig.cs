namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

public enum QueryConfigType
{
    Parameter = 100,
    Reference = 200
}

public interface IQueryConfig
{
    public QueryConfigType QueryConfigType { get; set; }
}