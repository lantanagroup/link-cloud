using LantanaGroup.Link.Shared.Application.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
public enum RequestStatus
{
    [StringValue("Pending")]
    Pending,
    [StringValue("Ready")]
    Ready,
    [StringValue("Processing")]
    Processing,
    [StringValue("Completed")]
    Completed,
    [StringValue("Failed")]
    Failed
}
