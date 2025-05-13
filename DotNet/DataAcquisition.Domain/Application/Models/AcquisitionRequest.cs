using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Application.Models;
public record AcquisitionRequest(string logId, string facilityId);
