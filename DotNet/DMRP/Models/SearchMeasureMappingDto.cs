using System.Runtime.Serialization;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DMRP.Models
{
    [DataContract]
    public class SearchMeasureMappingDto
    {
        [DataMember]
        public string? SortBy { get; set; }
        [DataMember]
        public SortOrder? SortOrder { get; set; }
        [DataMember]
        public int PageSize { get; set; } = 10;
        [DataMember]
        public int PageNumber { get; set; } = 1;
        [DataMember]
        public string? Measure { get; set; }
        [DataMember]
        public string? DQM { get; set; }
        [DataMember]
        public Frequency? Frequency { get; set; }

        public void Sanitize()
        {
            SortBy = SortBy?.Sanitize();
            Measure = Measure?.Sanitize();
            DQM = DQM?.Sanitize();

            if (PageSize < 1 || PageSize > 100)
            {
                PageSize = 10;
            }

            if (PageNumber < 1)
            {
                PageNumber = 1;
            } 
        }
    }
}