using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;

using Quartz;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.Tenant.Utils
{
    public class Helper
    {

        public static AuditEventMessage CreateFacilityAuditEvent(FacilityConfigModel facility)
        {
            AuditEventMessage auditEvent = new AuditEventMessage();
            auditEvent.FacilityId = facility.FacilityId;
            auditEvent.ServiceName = TenantConstants.ServiceName;
            auditEvent.EventDate = DateTime.UtcNow;
            auditEvent.User = "SystemUser";
            auditEvent.Action = AuditEventType.Create;
            auditEvent.Resource = typeof(FacilityConfigModel).Name;
            auditEvent.Notes = $"New facility configuration ({facility.Id}) created for '{facility.FacilityId}'";
            auditEvent.CorrelationId = Guid.NewGuid().ToString();
            return auditEvent;
        }

        public static AuditEventMessage UpdateFacilityAuditEvent(FacilityConfigModel updatedfacility, FacilityConfigModel existingFacility)
        {
            CompareLogic compareLogic = new CompareLogic();
            compareLogic.Config.MaxDifferences = 1000;
            var result = compareLogic.Compare(updatedfacility, existingFacility);
            List<Difference> list = result.Differences;
            AuditEventMessage auditEventLocal = new AuditEventMessage();
            auditEventLocal.PropertyChanges = new List<PropertyChangeModel>();
            list.ForEach(d =>
            {
                if (d.PropertyName == "CreatedOn" || d.PropertyName == "LastModifiedOn" || d.PropertyName == "MRPCreatedDate" || d.PropertyName == "MRPModifyDate") return;
                auditEventLocal.PropertyChanges.Add(new PropertyChangeModel
                {
                    PropertyName = d.PropertyName,
                    InitialPropertyValue = d.Object2Value,
                    NewPropertyValue = d.Object1Value
                });

            });
            AuditEventMessage auditEvent = auditEventLocal;
            auditEvent.FacilityId = updatedfacility.FacilityId;
            auditEvent.ServiceName = TenantConstants.ServiceName;
            auditEvent.EventDate = DateTime.UtcNow;
            auditEvent.User = "SystemUser";
            auditEvent.Action = AuditEventType.Update;
            auditEvent.Resource = typeof(FacilityConfigModel).Name;
            auditEvent.Notes = $"Updated facility configuration ({updatedfacility.Id}) for '{updatedfacility.FacilityId}'. Differences are {result.DifferencesString}";
            auditEvent.CorrelationId = Guid.NewGuid().ToString();
            return auditEvent;
        }

        public static AuditEventMessage DeleteFacilityAuditEvent(FacilityConfigModel facility)
        {
            AuditEventMessage auditEvent = new AuditEventMessage();
            auditEvent.FacilityId = facility.FacilityId;
            auditEvent.ServiceName = TenantConstants.ServiceName;
            auditEvent.EventDate = DateTime.UtcNow;
            auditEvent.User = "SystemUser";
            auditEvent.Action = AuditEventType.Delete;
            auditEvent.Resource = typeof(FacilityConfigModel).Name;
            auditEvent.Notes = $"Deleted facility configuration ({facility.Id}) for '{facility.FacilityId}'";
            auditEvent.CorrelationId = Guid.NewGuid().ToString();
            return auditEvent;
        }

        public static bool ValidateTopic(string strTopic, List<KafkaTopic> topics)
        {
            KafkaTopic topic;
            if (!Enum.TryParse(strTopic, out topic)) return false;
            if (!topics.Contains(topic)) return false;
            return true;
        }

        public static bool IsValidSchedule(string schedule)
        {
            var valid = CronExpression.IsValidExpression(schedule);
            // Some expressions are parsed as valid by the above method but they are not valid, like "* * * ? * *&54".
            //In order to avoid such invalid expressions an additional check is required, that is done using the below regex.

            var regex = @"^\s*($|#|\w+\s*=|(\?|\*|(?:[0-5]?\d)(?:(?:-|\/|\,)(?:[0-5]?\d))?(?:,(?:[0-5]?\d)(?:(?:-|\/|\,)(?:[0-5]?\d))?)*)\s+(\?|\*|(?:[0-5]?\d)(?:(?:-|\/|\,)(?:[0-5]?\d))?(?:,(?:[0-5]?\d)(?:(?:-|\/|\,)(?:[0-5]?\d))?)*)\s+(\?|\*|(?:[01]?\d|2[0-3])(?:(?:-|\/|\,)(?:[01]?\d|2[0-3]))?(?:,(?:[01]?\d|2[0-3])(?:(?:-|\/|\,)(?:[01]?\d|2[0-3]))?)*)\s+(\?|\*|(?:0?[1-9]|[12]\d|3[01])(?:(?:-|\/|\,)(?:0?[1-9]|[12]\d|3[01]))?(?:,(?:0?[1-9]|[12]\d|3[01])(?:(?:-|\/|\,)(?:0?[1-9]|[12]\d|3[01]))?)*)\s+(\?|\*|(?:[1-9]|1[012])(?:(?:-|\/|\,)(?:[1-9]|1[012]))?(?:L|W)?(?:,(?:[1-9]|1[012])(?:(?:-|\/|\,)(?:[1-9]|1[012]))?(?:L|W)?)*|\?|\*|(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)(?:(?:-)(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))?(?:,(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)(?:(?:-)(?:JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))?)*)\s+(\?|\*|(?:[0-6])(?:(?:-|\/|\,|#)(?:[0-6]))?(?:L)?(?:,(?:[0-6])(?:(?:-|\/|\,|#)(?:[0-6]))?(?:L)?)*|\?|\*|(?:MON|TUE|WED|THU|FRI|SAT|SUN)(?:(?:-)(?:MON|TUE|WED|THU|FRI|SAT|SUN))?(?:,(?:MON|TUE|WED|THU|FRI|SAT|SUN)(?:(?:-)(?:MON|TUE|WED|THU|FRI|SAT|SUN))?)*)(|\s)+(\?|\*|(?:|\d{4})(?:(?:-|\/|\,)(?:|\d{4}))?(?:,(?:|\d{4})(?:(?:-|\/|\,)(?:|\d{4}))?)*))$";

            return valid && Regex.IsMatch(schedule, regex);
        }

    }
}
