using LantanaGroup.Link.DataAcquisition.Domain.Settings;

namespace DataAcquisition.Domain.Settings;
public class GeneralSettings
{
    public int? MaxRetries { get; set; } = DataAcquisitionConstants.GeneralDataSettings.DefaultMaxRetries;
    public int? RetryDelayMinutes { get; set; } = DataAcquisitionConstants.GeneralDataSettings.DefaultRetryDelayMinutes;
}
