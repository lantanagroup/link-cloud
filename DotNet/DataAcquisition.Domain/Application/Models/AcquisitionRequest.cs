using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
public record AcquisitionRequest(string logId, string facilityId);
