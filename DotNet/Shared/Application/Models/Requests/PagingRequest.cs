using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Requests;

[DataContract]
public class PagingRequest
{
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 100;

    [DefaultValue(DefaultPageSize)]
    [Range(1, MaximumPageSize)]
    [DataMember]
    public int PageSize { get; set; } = DefaultPageSize;

    [DefaultValue(1)]
    [DataMember]
    public int PageNumber { get; set; } = 1;
}