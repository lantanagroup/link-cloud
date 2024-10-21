﻿using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[DataContract]
[Table("fhirListConfiguration")]
public class FhirListConfiguration : BaseEntityExtended
{
    [DataMember]
    public string FacilityId { get; set; }
    [DataMember]
    public string FhirBaseServerUrl { get; set; }
    [DataMember]
    public AuthenticationConfiguration? Authentication { get; set; }
    [DataMember]
    public List<EhrPatientList> EHRPatientLists { get; set; }
}