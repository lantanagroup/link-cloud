﻿using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[BsonCollection("fhirListConfiguration")]
public class FhirListConfiguration : BaseEntity
{
    public string FacilityId { get; set; }
    public string FhirBaseServerUrl { get; set; }
    public AuthenticationConfiguration Authentication { get; set; }
    public List<EhrPatientList> EHRPatientLists { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime ModifyDate { get; set; }
}
