using System;

namespace LantanaGroup.Link.Shared.Application.Models.Tenant
{
    public class TenantScheduledReportConfig
    {
        public string[] Daily { get; set; } = Array.Empty<string>();

        public string[] Weekly { get; set; } = Array.Empty<string>();

        public string[] Monthly { get; set; } = Array.Empty<string>();
    }

}
