﻿using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("queryPlan")]
public class QueryPlan : BaseEntityExtended
{
    public string PlanName { get; set; }
    public string ReportType { get; set; }
    public string FacilityId { get; set; }
    public string EHRDescription { get; set; }
    public string LookBack { get; set; }
    public Dictionary<string, IQueryConfig> InitialQueries { get; set; }
    public Dictionary<string, IQueryConfig> SupplementalQueries { get; set; }

    public QueryPlan() : base()
    {

    }
}