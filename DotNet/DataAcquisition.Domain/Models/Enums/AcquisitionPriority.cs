using LantanaGroup.Link.Shared.Application.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Models.Enums;
public enum AcquisitionPriority
{
    [StringValue("Normal")]
    Normal,
    [StringValue("High")]
    High,
    [StringValue("Critical")]
    Critical
}
