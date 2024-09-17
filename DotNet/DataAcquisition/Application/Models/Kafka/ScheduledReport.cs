﻿using LantanaGroup.Link.DataAcquisition.Domain.Models;

namespace LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;

public class ScheduledReport
{
    public string ReportType { get; set; }
    public Frequency Frequency { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
}
