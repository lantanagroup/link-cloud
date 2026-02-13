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
}
