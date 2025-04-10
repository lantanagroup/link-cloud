using LantanaGroup.Link.Shared.Application.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Models.Enums;
public enum QueryPhase
{
    [StringValue("Initial")]
    Initial,
    [StringValue("Supplemental")]
    Supplemental,
    [StringValue("Referential")]
    Referential,
    [StringValue("Polling")]
    Polling,
    [StringValue("Monitoring")]
    Monitoring
}
