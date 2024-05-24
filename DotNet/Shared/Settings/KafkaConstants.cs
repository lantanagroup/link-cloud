namespace LantanaGroup.Link.Shared.Settings
{
    public class KafkaConstants
    {
        public static string SectionName = "KafkaConnection";

        public static class HeaderConstants
        {
            public const string CorrelationId = "X-Correlation-Id";
            public const string RetryCount = "X-Retry-Count";
            public const string ExceptionFacilityId = "X-Exception-Facility-Id";
            public const string ExceptionService = "X-Exception-Service";
            public const string ExceptionMessage = "X-Exception-Message";
            public const string RetryExceptionMessage = "X-Retry-Exception-Message";
        }
    }
}
