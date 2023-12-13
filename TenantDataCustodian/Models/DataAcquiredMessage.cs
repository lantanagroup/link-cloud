namespace TenantDataCustodian.Models
{
    public class DataAcquiredMessage
    {
        public string TenantId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Provenance { get; set; }
        public object Data { get; set; }
    }
}
