namespace LantanaGroup.Link.Notification.Settings
{
    public class NotificationConstants
    {
        public const string ServiceName = "Notification";        

        public static class AppSettingsSectionNames
        {
            public const string ExternalConfigurationSource = "ExternalConfigurationSource";
            public const string ServiceInformation = "ServiceInformation";
            public const string Kafka = "KafkaConnection";
            public const string DatabaseProvider = "DatabaseProvider";
            public const string Smtp = "SmtpConnection";
            public const string Channels = "Channels";
            public const string IdentityProvider = "IdentityProviderConfig";
            public const string Serilog = "Serilog";
            public const string EnableSwagger = "EnableSwagger";
        }

        public static class NotificationLoggingIds
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
            public const int GetNotificationById = 1010;
            public const int GetNotificationByIdException = 1011;
            public const int GetNotificationByFacilityId = 1012;
            public const int GetNotificationByFacilityIdException = 1013;
            public const int GetNotificationConfigurationById = 1014;
            public const int GetNotificationConfigurationByIdException = 1015;
            public const int NotificationConfigurationCreation = 1016;
            public const int NotificationConfigurationCreationException = 1017;
            public const int NotificationCreation = 1018;
            public const int NotificationCreationException = 1019;
            public const int NotificationUpdate = 1020;
            public const int NotificationUpdateException = 1021;
            public const int NotificationDelete = 1022;
            public const int NotificationDeleteException = 1023;
            public const int NotificationChannel = 1024;
            public const int NotificationChannelException = 1025;
            public const int NotificationChannelNotFound = 1026;
            public const int NotificationChannelUpdate = 1027;
            public const int NotificationChannelUpdateException = 1028;
            public const int NotificationChannelDelete = 1029;
            public const int NotificationChannelDeleteException = 1030;
            public const int NotificationChannelNotFoundException = 1031;
            public const int NotificationChannelCreation = 1032;
            public const int NotificationChannelCreationException = 1033;
            public const int NotificationChannelList = 1034;
            public const int NotificationChannelListException = 1035;
            public const int NotificationChannelListQuery = 1036;
            public const int NotificationChannelListQueryException = 1037;
            public const int NotificationChannelListQueryExceptionMessage = 1038;
            public const int NotificationListQuery = 1039;
            public const int NotificationListQueryException = 1040;
            public const int NotificationConfigurationListQuery = 1041;
            public const int NotificationConfigurationListQueryException = 1042;
            public const int InvalidNotificationConfigurationCreationRequestWarning = 1043;
            public const int NotificationConfigurationUpdate = 1044;
            public const int NotificationConfigurationUpdateException = 1045;
            public const int NotificationConfigurationDelete = 1046;
            public const int NotificationConfigurationDeleteException = 1047;
            public const int InvalidNotificationConfigurationUpdateRequestWarning = 1048;
            public const int GetNotificationConfigurationByIdWarning = 1049;
            public const int GetNotificationConfigurationByIdExceptionWarning = 1050;
            public const int GetNotificationConfigurationByFacilityId = 1051;
            public const int GetNotificationConfigurationByFacilityIdWarning = 1052;    
            public const int GetNotificationConfigurationByFacilityIdException = 1053;
            public const int NotificationConfigurationDeleteWarning = 1054;
            public const int SendNotification = 1055;
            public const int SendNotificationWarning = 1056;
            public const int SendNotificationException = 1057;
            public const int EventConsumerInit = 2000;
            public const int EventConsumerObserved = 2001;
            public const int EventConsumerException = 2002;
            public const int EventConsumerOperationCanceled = 2003;   
            public const int EventConsumerInvalidEmailAddress = 2004;
            public const int KafkaProducer = 2004;
            public const int EmailChannel = 3000;
            public const int HealthCheck = 9000;            
        }

    }
}
