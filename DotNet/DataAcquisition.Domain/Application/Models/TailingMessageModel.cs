using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class TailingMessageModel
{
    public string Key { get; set; } = string.Empty;
    public ResourceAcquired ResourceAcquired { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public List<string>? LogIds { get; set; } = new List<string>();
}
