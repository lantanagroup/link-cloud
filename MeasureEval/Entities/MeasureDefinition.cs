﻿using Hl7.Fhir.Model;
using MongoDB.Bson.Serialization.Attributes;
using LantanaGroup.Link.MeasureEval.Serializers;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Attributes;

namespace LantanaGroup.Link.MeasureEval.Entities
{
    [BsonCollection("measureDefinition")]
    public class MeasureDefinition : BaseEntity
    {
        public string   measureDefinitionId { get; set; } = null!;
        public string?  measureDefinitionName { get; set; } = null!;
        [BsonSerializer(typeof(BundleSerDes))]
        public Bundle? bundle { get; set; } = null!;
        public string?  url { get; set; } = null!;
        public DateTime lastUpdated { get; set; } = DateTime.Now;
    }

}
