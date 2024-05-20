namespace LantanaGroup.Link.Census.Application.Settings;

public static class CensusConstants
{
    public const string ServiceName = "Census";
    public static class AppSettings
    {
        public const string ServiceInformation = "ServiceInformation";
        public const string DatabaseProvider = "DatabaseProvider";
        public const string ExternalConfigurationSource = "ExternalConfigurationSource";
    }

    public static class Scheduler
    {
        public const string Facility = "Facility";
        public const string ReportType = "ReportType";
        public const string JobTrigger = "JobTrigger";
    }

    public static class HeaderNames
    {
        public const string CorrelationId = "X-Correlation-Id";
    }

    public static class MessageNames
    {
        public const string PatientIDsAcquired = "PatientIDsAcquired";
        public const string PatientIDsAcquiredError = "PatientIDsAcquired-Error";
    }

    public static class CensusLoggingIds
    {
        public const int GenerateItems = 1000;
        public const int ListItems = 1001;
        public const int GetItem = 1002;
        public const int InsertItem = 1003;
        public const int UpdateItem = 1004;
        public const int DeleteItem = 1005;
        public const int GetItemNotFound = 1006;
        public const int UpdateItemNotFound = 1007;
        public const int KafkaConsumer = 10008;
        public const int KafkaProducer = 10009;
        public const int HealthCheck = 10010;
    }
}
