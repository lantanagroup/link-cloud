namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka
{
    public interface IBaseDataAcqMessage
    {
        string TenantId { get; set; }
        string FacilityId { get; set; }
        List<DataAcquisitionTypes> Type { get; set; }
    }
}
