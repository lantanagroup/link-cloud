using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.DataAcquisition.Domain.Settings;

public class AcquisitionWorkerProcessorSettings
{
    public int MaxConcurrentAcquisitions { get; set; } = 8;
    public int WorkChannelCapacity { get; set; } = 200;
    public int MaxBatchesPerFacilityPerRun { get; set; } = 40;
    public int MaxBatchesFailStalledPerRun { get; set; } = 20;
    public int TimeBudgetPerRunSeconds { get; set; } = 60;
    public int StalledQueuedThresholdMinutes { get; set; } = 15;
    public int StalledProcessingThresholdMinutes { get; set; } = 240;
}
