using System.ComponentModel;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.Shared.Application.Enums
{
    [DataContract]
    public enum SortOrder
    {
        [DataMember]
        [Description("Ascending")]
        Ascending,
        [DataMember]
        [Description("Descending")]
        Descending
    }
}
