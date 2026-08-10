using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.DMRP.Models
{
    public class SearchMeasureMappingDto
    {
        public string? SortBy { get; set; }
        public SortOrder? SortOrder { get; set; }
        public int PageSize { get; set; } = 10;
        public int PageNumber { get; set; } = 1;
        public string? Measure { get; set; }
        public string? DQM { get; set; }
        public Frequency? Frequency { get; set; }

        public void Sanitize()
        {
            SortBy = SortBy?.Sanitize();

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