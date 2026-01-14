using LantanaGroup.Link.Shared.Application.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
public enum AcquisitionPriority
{
    [StringValue("Normal")]
    Normal = 2,
    [StringValue("High")]
    High = 1,
    [StringValue("Critical")]
    Critical = 0
}
