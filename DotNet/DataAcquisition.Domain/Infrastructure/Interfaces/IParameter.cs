using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;

public enum ParameterType
{
    Literal = 100,
    ResourceIds = 200,
    Variable = 300,

}

public interface IParameter
{
    public ParameterType ParameterType { get; set; }
}