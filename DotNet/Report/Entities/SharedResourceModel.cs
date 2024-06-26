﻿using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Attributes;
using MongoDB.Bson.Serialization.Attributes;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Report.Domain.Enums;

namespace LantanaGroup.Link.Report.Entities
{
    [BsonCollection("sharedResource")]
    public class SharedResourceModel : ReportEntity, IFacilityResource
    {
        public string FacilityId { get; set; }
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        [BsonSerializer(typeof(MongoFhirBaseSerDes<Resource>))]
        public Resource Resource { get; set; }

        public string GetId()
        {
            return this.Id;
        }

        public Resource GetResource()
        {
            return Resource;
        }
    }
}
