using LantanaGroup.Link.Shared.Application.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Models.Enums;
public enum RequestStatus
{
    [StringValue("Pending")]
    Pending,
    [StringValue("Processing")]
    Processing,
    [StringValue("Completed")]
    Completed,
    [StringValue("Failed")]
    Failed
}
