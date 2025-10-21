namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

public class AcquisitionJobSettings
{
    public const string SectionName = "AcquisitionJobSettings";

    public string CronSchedule { get; set; } = "0/10 * * * * ?";
}
