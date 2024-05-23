namespace LantanaGroup.Link.Audit.Settings
{
    public class AuditConstants
    {
        public const string ServiceName = "Audit";

        public static class AppSettingsSectionNames
        {
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string ServiceInformation = "ServiceInformation";
            public const string DatabaseProvider = "DatabaseProvider";            
            public const string IdentityProvider = "IdentityProviderConfig";
            public const string Serilog = "Serilog";
            public const string EnableSwagger = "EnableSwagger";
        }

        public static class AuditExceptionMessages 
        {
            public const string NullOrWhiteSpaceFacilityId = "No facility id was given and is required for Audit events.";
            public const string NullOrWhiteSpaceServiceName = "No service name was given and is required for Audit events.";
        }

        public static class AuditLoggingIds 
        {
            public const int GenerateItems = 1000;
            public const int SearchPerformed = 1001;
            public const int GetItem = 1002;
            public const int InsertItem = 1003;
            public const int UpdateItem = 1004;
            public const int DeleteItem = 1005;      
            public const int GetItemNotFound = 1006;
            public const int UpdateItemNotFound = 1007;
            public const int SearchException = 1008;
            public const int GetItemException = 1009;
            public const int GetFacilityAuditEventsQuery = 1010;
            public const int GetFacilityAuditEventsQueryException = 1011;
            public const int EventConsumerInit = 2000;
            public const int EventConsumerObserved = 2001;
            public const int EventConsumerException = 2002;
            public const int EventConsumerOperationCanceled = 2003;
            public const int DeadLetterException = 2004;
            public const int TransientException = 2005;
            public const int HealthCheck = 9000;
        }
    }
}
