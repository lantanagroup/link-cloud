﻿using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Application.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportableEvent
{
    [StringValue("Discharge")]
    Discharge,
    [StringValue("EOM")]
    EOM,
    [StringValue("EOW")]
    EOW,
    [StringValue("EOD")]
    EOD
}
