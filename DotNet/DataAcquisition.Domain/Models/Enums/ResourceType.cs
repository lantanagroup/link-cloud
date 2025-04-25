using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Models.Enums;
public enum ResourceType
{
    PatientList,
    Encounter,
    Condition,
    MedicationRequest,
    Observation,
    Procedure,
    ServiceRequest,
    Coverage,
    MedicationAdminisitration
}
